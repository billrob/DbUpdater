using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbUpdater
{
	public class ValidationException : ApplicationException
	{
		public ValidationException():base() 
		{
		}
		public ValidationException(string message)
			: base(message)
		{
		}
		public ValidationException(string message, Exception inner)
			: base(message, inner)
		{
		}
	}
}
