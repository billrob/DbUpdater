using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbUpdater.Tests.EnvironmentFlagTests
{
	[TestFixture]
	public class FilterForEnvironmentFlagsTests
	{
		private Options _options;
		private List<string> _lines;
		private string[] _response;
		private const string _goodString = "good text";
		private const string _otherGoodString = "other good text";

		[SetUp]
		public void SetUp()
		{
			_response = null;
			_options = new Options()
			{
				Environment = "test",
			};

			_lines = new List<string>()
			{
				"<environment>",
				"<test>",
				_goodString,
				_goodString,
				"</test>",
				"<otherTest>",
				_otherGoodString,
				"</otherTest>",
				"</environment>",
			};
		}

		[Test]
		public void when_empty_should_return_empty()
		{
			_lines = new List<string>();

			Execute();

			Assert.AreEqual(0, _response.Length);
		}

		[Test]
		public void when_no_environment_should_return_same()
		{
			_options.Environment = null;

			Execute();

			Assert.AreEqual(_lines.Count, _response.Length);
		}

		[Test]
		public void when_no_environment_match_should_return_empty()
		{
			_options.Environment = "somethingElse";

			Execute();

			Assert.AreEqual(0, _response.Length);
		}

		[Test]
		public void when_no_environment_xml_tag_should_return_same()
		{
			var startCount = _lines.Count;
			_lines.RemoveAt(0);

			Execute();

			Assert.AreEqual(startCount - 1, _response.Length);
		}

		[Test]
		public void when_root_node_not_environment_should_exception()
		{
			_lines.Insert(0, "<bob>");
			_lines.Add("</bob>");

			Assert.Throws<EnvironmentRootNodeMissingException>(() => Execute());
		}

		[Test]
		public void simple_case_should_work()
		{
			Execute();

			Assert.AreEqual(2, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
			Assert.AreEqual(_goodString, _response[1]);
		}

		[Test]
		public void simple_case_should_work_ignoring_casing()
		{
			_options.Environment = _options.Environment.ToUpper();

			Execute();

			Assert.AreEqual(2, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
			Assert.AreEqual(_goodString, _response[1]);
		}

		[Test]
		public void simple_other_case_should_work()
		{
			_options.Environment = "otherTest";

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_otherGoodString, _response[0]);
		}

		[Test]
		public void simple_other_case_should_work_ignoring_casing()
		{
			_options.Environment = "otherTest".ToUpper();

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_otherGoodString, _response[0]);
		}

		[Test]
		public void when_bad_xml_should_throw_exception()
		{
			_lines.RemoveAt(1);

			Assert.Throws<System.Xml.XmlException>(() => Execute());
		}

		[Test]
		public void when_nested_xml_nodes()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test><test1>" + _goodString + "</test1></test>",
				"</environment>",
			};

			Assert.Throws<OnlyOneLevelOfXmlNodeException>(() => Execute());
		}

		[Test]
		public void should_match_multiple()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test>" + _goodString + "</test>",
				"<test>" + _goodString + "</test>",
				"</environment>",
			};

			Execute();

			Assert.AreEqual(2, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
			Assert.AreEqual(_goodString, _response[1]);
		}

		[Test]
		public void environment_casing_should_not_matter()
		{
			_lines = new List<string>()
			{
				"<ENVIRONMENT>",
				"<test>" + _goodString + "</test>",
				"</ENVIRONMENT>",
			};

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
		}

		[Test]
		public void new_line_edge_case1()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test>" + _goodString + "</test>",
				"</environment>",
			};

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
		}

		[Test]
		public void new_line_edge_case2()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test>" + _goodString,
				"</test>",
				"</environment>",
			};

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
		}

		[Test]
		public void new_line_edge_case3()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test>",
				_goodString + "</test>",
				"</environment>",
			};

			Execute();

			Assert.AreEqual(1, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
		}

		[Test]
		public void new_line_edge_case4()
		{
			_lines = new List<string>()
			{
				"<environment>",
				"<test>",
				_goodString,
				_goodString + "</test>",
				"</environment>",
			};

			Execute();

			Assert.AreEqual(2, _response.Length);
			Assert.AreEqual(_goodString, _response[0]);
			Assert.AreEqual(_goodString, _response[1]);
		}

		private void Execute()
		{
			_response = Program.FilterForEnvironmentFlags(_lines.ToArray(), _options);
		}
	}
}
