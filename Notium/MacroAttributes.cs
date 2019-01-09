using System;
	
namespace Notium.Models
{
	[AttributeUsage (AttributeTargets.All, AllowMultiple = true)]
	public class MacroAttribute : Attribute
	{
		public string Name { get; set; }
		public MacroAttribute (string name) => Name = name;
	}

	public class MacroSuffixAttribute : Attribute
	{
		public string Suffix { get; set; }
		public MacroSuffixAttribute (string suffix) => Suffix = suffix;
	}

	public class MacroNoteAttribute : Attribute
	{
		public string Name { get; set; }
		public MacroNoteAttribute (string name) => Name = name;
	}

	public class MacroRelativeAttribute : Attribute
	{
		public string Name { get; set; }
		public string Increase { get; set; }
		public string Decrease { get; set; }

		public MacroRelativeAttribute (string name, string increase, string decrease)
		{
			Name = name;
			Increase = increase;
			Decrease = decrease;
		}
	}
}
