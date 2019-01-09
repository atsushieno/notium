using System;
using System.Linq;
using Commons.Music.Midi;

namespace Notium.Models
{
	public class RawMidiProcessor : PrimitiveProcessor
	{
		public RawMidiProcessor ()
			: this (MidiAccessManager.Default)
		{
		}

		public RawMidiProcessor (IMidiAccess access)
			: this (access.OpenOutputAsync (access.Outputs.First ().Id).Result)
		{
		}

		public RawMidiProcessor (IMidiOutput output)
		{
			this.output = output;
		}

		IMidiOutput output;

		public override void BeginLoop (int channel)
		{
			throw new NotSupportedException ();
		}

		public override void BreakLoop (int channel, params int [] targets)
		{
			throw new NotSupportedException ();
		}

		public override void Debug (object o)
		{
			throw new NotImplementedException ();
		}

		public override void EndLoop (int channel, int repeats)
		{
			throw new NotSupportedException ();
		}

		byte [] buffer;
		public override void MidiEvent (int channel, byte statusCode, byte data)
		{
			buffer [0] = (byte)(statusCode + channel);
			buffer [1] = data;
			output.Send (buffer, 0, 2, 0);
		}

		public override void MidiEvent (int channel, byte statusCode, byte data1, byte data2)
		{
			buffer [0] = (byte)(statusCode + channel);
			buffer [1] = data1;
			buffer [2] = data2;
			output.Send (buffer, 0, 3, 0);
		}

		public override void MidiMeta (int metaType, params byte [] bytes)
		{
			// nothing to do here.
		}

		public override void MidiMeta (int metaType, string data)
		{
			// nothing to do here.
		}

		public override void MidiSysex (byte [] bytes, int offset, int length)
		{
			output.Send (bytes, offset, length, 0);
		}
	}
}
