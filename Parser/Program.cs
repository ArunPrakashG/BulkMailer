using System;
using System.IO;
using System.Threading.Tasks;

namespace Parser
{
	internal class Program
	{
		private const string MAILID_PATH = "mails.txt";
		private const string MAIL_NAME_PATH = "names.txt";
		private static string[] Mails;
		private const string RESULT_PATH = "mailnames.txt";


		private static async Task Main(string[] args)
		{
			if (!File.Exists(MAILID_PATH) || !File.Exists(MAIL_NAME_PATH))
			{
				Console.WriteLine("Files doesn't exist.");
				await Task.Delay(1000);
				return;
			}

			try
			{
				string[] mailIds = File.ReadAllLines(MAILID_PATH);
				string[] mailNames = File.ReadAllLines(MAIL_NAME_PATH);

				if (mailIds == null || mailIds.Length <= 0 || mailNames == null || mailNames.Length <= 0)
				{
					await Task.Delay(1000);
					return;
				}

				if (mailIds.Length != mailNames.Length)
				{
					Console.WriteLine("Counts doesn't match.");
					await Task.Delay(1000);
					return;
				}

				int mailsCount = mailIds.Length;
				int mailNamesCount = mailNames.Length;

				Mails = new string[mailsCount];
				Console.WriteLine($"Total mail count: {mailsCount} | Total mail names count: {mailNamesCount}");

				for (int i = 0; i < mailsCount; i++)
				{
					string mailId = mailIds[i];
					string mailName = mailNames[i];

					if (string.IsNullOrEmpty(mailId) || string.IsNullOrEmpty(mailName))
					{
						continue;
					}

					if (!IsRepeat(mailId, mailName))
					{
						Mails[i] = $"{mailId}|{mailName}";
						Console.WriteLine($"Parsed {mailId} / {mailName} / {i}");
					}
				}

				if (Mails.Length != mailsCount)
				{
					Console.WriteLine("Counts doest match up.");
				}

				File.WriteAllLines(RESULT_PATH, Mails);
				Console.WriteLine($"Successfully written '{Mails.Length}' mail ids to '{RESULT_PATH}' !");
				Console.ReadKey();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				await Task.Delay(10000);
				return;
			}
		}

		private static bool IsRepeat(string mailId, string mailName)
		{
			if (string.IsNullOrEmpty(mailId) || string.IsNullOrEmpty(mailName))
			{
				return true;
			}

			if (Mails == null || Mails.Length <= 0)
			{
				return false;
			}

			for(int i=0; i< Mails.Length; i++)
			{
				if (string.IsNullOrEmpty(Mails[i]))
				{
					continue;
				}

				string[] split = Mails[i].Split('|');

				if(split == null || split.Length <= 0)
				{
					continue;
				}

				if(split[0].Equals(mailId, StringComparison.OrdinalIgnoreCase) || split[1].Equals(mailName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
