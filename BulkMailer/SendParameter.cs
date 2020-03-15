using System;
using System.Collections.Generic;
using System.Text;

namespace BulkMailer
{
	internal struct SendParameter
	{
		internal readonly string Name;
		internal readonly string To;
		internal readonly string? Body;

		internal SendParameter(string _name, string _to, string? _body = null)
		{
			Name = _name;
			To = _to;
			Body = _body;
		}
	}
}
