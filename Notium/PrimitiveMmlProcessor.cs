using System;
using System.IO;
using System.Linq;

namespace Notium.Models
{
	public class PrimitiveMmlProcessingContext : ControllerProcessingContext
	{
		PrimitiveProcessor primitive_controller;

		public PrimitiveMmlProcessingContext (TextWriter output, TextWriter errorOutput)
			: this (new PrimitiveMmlProcessor (output, errorOutput))
		{
		}

		public PrimitiveMmlProcessingContext (PrimitiveMmlProcessor primitive)
		{
			primitive_controller = primitive;
		}

		public override PrimitiveProcessor PrimitiveProcessor => primitive_controller;
	}

	public class PrimitiveMmlProcessor : PrimitiveProcessor
	{
		public PrimitiveMmlProcessor (TextWriter output, TextWriter debugOutput = null)
		{
			this.output = output;
			this.debug_output = debugOutput ?? Console.Error;
		}

		TextWriter output;
		TextWriter debug_output;

		public override void BeginLoop (int channel)
		{
			output.Write ("[");
		}

		public override void BreakLoop (int channel, params int [] targets)
		{
			output.Write (":" + string.Join (",", targets.Select (t => t.ToString ())));
		}

		public override void Debug (object o)
		{
			debug_output.WriteLine (o);
		}

		public override void EndLoop (int channel, int repeats)
		{
			output.Write ("]" + repeats);
		}

		public override void MidiEvent (int channel, byte statusCode, byte data)
		{
			output.Write ($"__MIDI {{ #${statusCode:X02},#{data:x02} }}");
		}

		public override void MidiEvent (int channel, byte statusCode, byte data1, byte data2)
		{
			output.Write ($"__MIDI {{ #{statusCode:X02}, #{data1:x02}, #{data2:x02} }} ");
		}

		public override void MidiMeta (int metaType, params byte [] bytes)
		{
			output.Write ($"__MIDI_META {{ #{metaType:X02}");
			foreach (var b in bytes) {
				output.Write (", #");
				output.Write (b.ToString ("x02"));
			}
			output.Write ("}} ");
		}

		public override void MidiMeta (int metaType, string data)
		{
			var escaped = data.Replace ("\\", "\\\\").Replace ("\"", "\\\"");
			output.Write ($"__MIDI_META {{ #{metaType:X02}, \"{escaped} }} ");
		}

		public override void MidiSysex (byte [] bytes, int offset, int length)
		{
			output.Write ($"__MIDI");
			output.Write ("#F0");
			foreach (var b in bytes.Skip (offset).Take (length)) {
				output.Write (", #{b:x02}");
			}
			output.Write ("} ");
		}
	}
}
