using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BulkMailer
{
	internal class Program
	{
		private static readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
		private const int DELAY_BETWEEN_MAIL = 10; // In seconds
		private const int MAX_MAILS_PER_DAY = 150;
		private static string? SEND_FROM_MAIL_ID; // will be set at runtime
		private static string? SEND_FROM_PASSWORD; // will be set at runtime
		private static string? SEND_FROM_NAME; // will be set at runtime
		private static string? SEND_BODY; // will be set at runtime
		private static string? SEND_SUBJECT; // will be set at runtime	
		private static readonly Dictionary<string, SendParameter> MailIdCollection = new Dictionary<string, SendParameter>();
		private const string MAIL_PATH = "mailnames.txt";
		private static readonly List<string> LogMessageCache = new List<string>();
		private const string LogFilePath = "log.txt";
		private static int SendCount = 0;
		private static DateTime LastRequestTime = DateTime.Now;

		private static async Task Main(string[] args)
		{
			Info($"--------------------[{nameof(BulkMailer)} | {Assembly.GetExecutingAssembly().GetName().Version}]--------------------");

			Info("Please enter the mail address from which all the mails have to be send: ");
			SEND_FROM_MAIL_ID = Console.ReadLine();
			Info("Password: ");
			SEND_FROM_PASSWORD = Console.ReadLine();
			Info("Enter the name which should be specified for this account: ");
			SEND_FROM_NAME = Console.ReadLine();
			Info("Enter the Body of the mail to be send: ");
			SEND_BODY = Console.ReadLine();
			Info("Enter the subject of the mail: ");
			SEND_SUBJECT = Console.ReadLine();

			if (string.IsNullOrEmpty(SEND_FROM_MAIL_ID) || string.IsNullOrEmpty(SEND_FROM_PASSWORD) ||
				string.IsNullOrEmpty(SEND_FROM_NAME) || string.IsNullOrEmpty(SEND_SUBJECT) || string.IsNullOrEmpty(SEND_BODY))
			{
				Error("Entered mail address/password/name/body/subject is invalid.");
				Info("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			if (!await CacheMailIds())
			{
				Info("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			DateTime startTime = DateTime.Now;			

			foreach (var emailPair in MailIdCollection)
			{
				if (string.IsNullOrEmpty(emailPair.Key))
				{
					continue;
				}

				if(string.IsNullOrEmpty(emailPair.Value.To) || string.IsNullOrEmpty(emailPair.Value.Name))
				{
					continue;
				}

				await Request(async () => await SendMail(emailPair.Value));
			}

			if (SendCount != MailIdCollection.Count)
			{
				Error($"'{MailIdCollection.Count - SendCount}' people were skipped due to incorrect address.");
			}

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"Successfully send emails to '{SendCount}' people in {Math.Round((DateTime.Now - startTime).TotalMinutes, 3)} minutes!");
			Console.ResetColor();

			try
			{
				File.WriteAllLines(LogFilePath, LogMessageCache);
			}
			catch (Exception)
			{
				// Ignored
			}

			Info("Press any key to exit...");
			Console.ReadKey();
		}

		private static async Task<bool> CacheMailIds()
		{
			if (!File.Exists(MAIL_PATH))
			{
				return false;
			}

			await Sync.WaitAsync().ConfigureAwait(false);

			try
			{
				string[] mails = File.ReadAllLines(MAIL_PATH);

				if (mails == null || mails.Length <= 0)
				{
					return false;
				}

				int readCount = mails.Length;
				int successCount = 0;
				for (int i = 0; i < readCount; i++)
				{
					if (string.IsNullOrEmpty(mails[i]))
					{
						continue;
					}

					string[] split = mails[i].Split('|');

					if (split == null || split.Length <= 0 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1]))
					{
						continue;
					}

					if (!MailIdCollection.ContainsKey(split[0]))
					{
						MailIdCollection.Add(split[0], new SendParameter(split[1], split[0], SEND_BODY));
						Info($"Cached {split[1]} / {split[0]}");
						successCount++;
					}
				}

				if(MailIdCollection.Count != successCount)
				{
					Error($"'{readCount - successCount}' mails have been ignored as they were invalid.");					
				}

				Info($"Cached email ids: {successCount} | read count: {readCount}");
				return true;
			}
			catch (Exception e)
			{
				Error($"{e.Message} / {e.StackTrace}");
				return false;
			}
			finally
			{
				Sync.Release();
			}
		}

		private static bool IsValidEmail(string? email)
		{
			if (string.IsNullOrEmpty(email))
			{
				return false;
			}

			try
			{
				var addr = new MailAddress(email);
				return addr.Address == email;
			}
			catch
			{
				return false;
			}
		}

		internal static async Task<bool> SendMail(SendParameter mail)
		{
			if (string.IsNullOrEmpty(mail.To) || string.IsNullOrEmpty(mail.Body) || string.IsNullOrEmpty(mail.Name))
			{
				return false;
			}

			if (!IsValidEmail(mail.To))
			{
				Error($"{mail.To} is an invalid email address.");
				return false;
			}

			await Sync.WaitAsync().ConfigureAwait(false);
			MailKit.Net.Smtp.SmtpClient? Client = null;

			try
			{
				Client = new MailKit.Net.Smtp.SmtpClient();
				MimeMessage message = new MimeMessage();
				message.From.Add(new MailboxAddress(SEND_FROM_NAME, SEND_FROM_MAIL_ID));
				message.To.Add(new MailboxAddress(mail.Name, mail.To));
				message.Subject = SEND_SUBJECT;

				message.Body = new TextPart()
				{
					Text = SEND_BODY
				};

				Client.MessageSent += OnMailSend;
				Client.ServerCertificateValidationCallback = (s, c, h, e) => true;
				await Client.ConnectAsync("smtp.gmail.com", 465, true).ConfigureAwait(false);
				await Client.AuthenticateAsync(SEND_FROM_MAIL_ID, SEND_FROM_PASSWORD).ConfigureAwait(false);

				if (!Client.IsAuthenticated)
				{
					Error("Client isnt authenticated.");
					return false;
				}

				LastRequestTime = DateTime.Now;
				await Client.SendAsync(message).ConfigureAwait(false);
				await Client.DisconnectAsync(true).ConfigureAwait(false);
				Info($"Message send -> {mail.To} / {mail.Name} | Index: {SendCount++}");
				return true;
			}
			catch (Exception e)
			{
				Error($"{e.Message} / {e.StackTrace}");
				return false;
			}
			finally
			{
				Sync.Release();
				Client?.Dispose();
			}
		}

		private static async Task<T> Request<T>(Func<Task<T>> function)
		{
			try
			{
				await Sync.WaitAsync().ConfigureAwait(false);

				if ((DateTime.Now - LastRequestTime).TotalSeconds <= DELAY_BETWEEN_MAIL)
				{
					await Task.Delay(TimeSpan.FromSeconds(DELAY_BETWEEN_MAIL));
				}

				return await function().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Error($"Request Exception -> {e.Message}");
				Console.WriteLine(e.Message);
				return default;
			}
			finally
			{
				Sync.Release();
			}
		}

		private static void OnMailSend(object? sender, MailKit.MessageSentEventArgs e)
		{
			if (sender == null || e == null)
			{
				return;
			}

			Trace($"[EVENT CONFIRMATION] | Successfully send to {e.Message.To} !");
		}

		internal static void Trace(string? msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				return;
			}
			
			LogMessageCache.Add($"{DateTime.Now.ToString()} | TRACE | {msg}");
		}

		internal static void Info(string? msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				return;
			}

			Console.WriteLine($"{DateTime.Now.ToString()} | {msg}");
			LogMessageCache.Add($"{DateTime.Now.ToString()} | INFO | {msg}");
		}

		internal static void Error(string? msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				return;
			}

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"{DateTime.Now.ToString()} | {msg}");
			LogMessageCache.Add($"{DateTime.Now.ToString()} | ERROR | {msg}");
			Console.ResetColor();
		}
	}
}
