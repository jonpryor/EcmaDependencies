using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;

class GetStandardizedTypes {

	static readonly string[] ExcludeTypes = new[]{
		"System.INullableValue",
	};

	static Dictionary<string, string> Replacements = new Dictionary<string, string> () {
		{ "System.Void", "void" },
	};

	public static void Main (string[] args)
	{
		if (args.Length == 0)
			args = new []{"CLILibraryTypes.xml"};

		var types = new HashSet<string> ();

		foreach (var f in args) {
			var d = XDocument.Load (f);
			foreach (var type in d.Elements ("Libraries")
					.Elements ("Types")
					.Where (lib => lib.Attribute ("Library").Value != "Parallel")
					.Elements ("Type")) {
				var fn = GetTypeName (type.Attribute ("FullName").Value);
				if (Array.BinarySearch (ExcludeTypes, fn) >= 0)
					continue;
				string r;
				if (Replacements.TryGetValue (fn, out r))
					fn = r;
				types.Add (fn);
			}
		}

		Console.WriteLine ("// GENERATED FILE");
		Console.WriteLine ();
		Console.WriteLine ("partial class Ecma335 {");
		Console.WriteLine ();
		Console.WriteLine ("\tpublic static readonly System.Type[] Types = new[]{");
		foreach (var t in types.OrderBy (s => s))
			Console.WriteLine ("\t\t\ttypeof ({0}),", t);
		Console.WriteLine ("\t};");
		Console.WriteLine ("}");
	}

	static string GetTypeName (string type)
	{
		var n = new StringBuilder (type.Length);
		var g = false;

		foreach (var c in type) {
			switch (c) {
				case '<':
					g = true;
					n.Append ('<');
					break;
				case '>':
					g = false;
					n.Append ('>');
					break;
				case ',':
					n.Append (',');
					break;
				default:
					if (!g)
						n.Append (c);
					break;
			}
		}

		return n.ToString ();
	}
}
