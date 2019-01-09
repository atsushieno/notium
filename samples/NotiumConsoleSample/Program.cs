using System;
using Notium.Models;

namespace Notium.Samples.ConsoleSample
{
	class MainClass
	{
		public static void Main (string [] args)
		{
			var p = new RawMidiProcessor ();
			var ctx = new SimpleControllerProcessingContext (p);
			var tp = new TrackController (ctx);
			tp.Channel = 0;
			tp.Note (0x40);
		}
	}
}
