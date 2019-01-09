using Notium.Models;
using NUnit.Framework;
using System;
using System.IO;

namespace Notium.Model.Tests
{
	[TestFixture ()]
	public class PrimitiveMmlProcessorTest
	{
		StringWriter writer;
		StringWriter error_writer;
		PrimitiveMmlProcessor mtc;
		TrackController tc;

		[SetUp]
		public void SetUp ()
		{
			writer = new StringWriter ();
			error_writer = new StringWriter ();
			mtc = new PrimitiveMmlProcessor (writer, error_writer);
			tc = new TrackController (new PrimitiveMmlProcessingContext (mtc));
		}

		[Test ()]
		public void PrimitiveLoop ()
		{
			mtc.BeginLoop (0);
			mtc.EndLoop (0, 2);
			Assert.AreEqual ("[]2", writer.ToString ());
		}

		[Test]
		public void SimpleLoop ()
		{
			tc.BeginLoop ();
			tc.EndLoop (2);
			Assert.AreEqual ("[]2", writer.ToString ());
		}

		[Test]
		public void SimpleNote ()
		{
			string expected = "__MIDI { #90, #40, #64 } __MIDI { #80, #40, #00 } ";
			tc.Note (0x40);
			// This isn't very good comparison... rewrite every time we change formatting.
			Assert.AreEqual (expected, writer.ToString ());
		}

		[Test]
		public void SpectraPitchBend ()
		{
			string expected = "__MIDI { #E0, #00, #00 } __MIDI { #E0, #b8, #ff } __MIDI { #E0, #d4, #fe } __MIDI { #E0, #92, #fe } __MIDI { #E0, #e0, #fd } __MIDI { #E0, #b8, #fd } __MIDI { #E0, #97, #fd } __MIDI { #E0, #fb, #fc } __MIDI { #E0, #e2, #fc } __MIDI { #E0, #cc, #fc } __MIDI { #E0, #b8, #fc } __MIDI { #E0, #a6, #fc } __MIDI { #E0, #96, #fc } __MIDI { #E0, #b8, #ff } ";
			tc.PitchBend.OneShot (0, -200, 0, new Length (4));
			Console.WriteLine (writer);
			Assert.AreEqual (expected, writer.ToString ());
		}
	}
}
