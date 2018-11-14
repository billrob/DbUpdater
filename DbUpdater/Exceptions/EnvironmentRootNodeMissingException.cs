using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbUpdater
{
	public class EnvironmentRootNodeMissingException : ApplicationException
	{
		public EnvironmentRootNodeMissingException()
			: base("The root node of the xml must be 'environment'")
		{

		}
	}
}
