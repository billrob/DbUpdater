using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbUpdater
{
	public class OnlyOneLevelOfXmlNodeException : ApplicationException
	{
		public OnlyOneLevelOfXmlNodeException(string nodeInViolation)
			: base("The xml node: " + nodeInViolation + " can not have any children.")
		{

		}
	}
}
