using System;

namespace Notium.Models
{
	public abstract class ControllerProcessingContext
	{
		public abstract PrimitiveProcessor PrimitiveProcessor { get; }
	}

	public class SimpleControllerProcessingContext : ControllerProcessingContext
	{
		public SimpleControllerProcessingContext (PrimitiveProcessor processor)
		{
			this.processor = processor;
		}

		PrimitiveProcessor processor;

		public override PrimitiveProcessor PrimitiveProcessor => processor;
	}

	public struct Length
	{
		public static int BaseCount { get; set; } = 192;
		public int Value { get; set; }

		public Length (int value) => Value = value;

		public static implicit operator Length (int value) => new Length { Value = value == 0 ? 0 : BaseCount / value };

		public static implicit operator int (Length value) => value.Value == 0 ? 0 : BaseCount / value.Value;
	}

	public abstract class PrimitiveProcessor
	{
		public abstract void Debug (object o);

		public abstract void MidiEvent (int channel, byte statusCode, byte data);

		public abstract void MidiEvent (int channel, byte statusCode, byte data1, byte data2);

		public abstract void MidiSysex (byte [] bytes, int offset, int length);

		public abstract void MidiMeta (int metaType, params byte [] bytes);

		public abstract void MidiMeta (int metaType, string data);

		public abstract void BeginLoop (int channel);

		public abstract void BreakLoop (int channel, params int [] targets);

		public abstract void EndLoop (int channel, int repeats);
	}

	public partial class TrackController
	{
		public TrackController (ControllerProcessingContext context)
		{
			this.context = context;
			InitializeFields ();
		}

		ControllerProcessingContext context;

		PrimitiveProcessor primitive => context.PrimitiveProcessor;

		#region Primitive length operators

		public int TimelinePosition { get; set; }

		public void Step (Length length)
		{
			TimelinePosition += length;
		}

		public void JumpTo (Length length)
		{
			TimelinePosition = length;
		}

		public void Rewind (Length length)
		{
			TimelinePosition -= length;
		}

		#endregion // Primitive length operators

		public byte Channel { get; set; }

		[Macro ("CH")]
		public void SetChannelByNaturalNumber (byte channelByNaturalNumber) => Channel = (byte)(channelByNaturalNumber - 1);

		[Macro ("DEBUG")]
		public void Debug (object val) => primitive.Debug (val);

		[Macro ("ASSERT_STEP")]
		public void AssetStep (Length expected, string label)
		{
			if (expected != TimelinePosition)
				primitive.Debug ($"WARNING: step assertion failed: {label} (expected: {expected}, actual: {(int)TimelinePosition})");
		}

		#region MIDI operators

		public void MidiNoteOff (byte key, byte velocity)
		{
			primitive.MidiEvent (Channel, 0x80, key, velocity);
		}

		public void MidiNoteOn (byte key, byte velocity)
		{
			primitive.MidiEvent (Channel, 0x90, key, velocity);
		}

		public void MidiPAf (byte key, byte velocity)
		{
			primitive.MidiEvent (Channel, 0xA0, key, velocity);
		}

		public void MidiCC (byte opcode, byte operand)
		{
			primitive.MidiEvent (Channel, 0xB0, opcode, operand);
		}

		public void MidiProgramChange (byte program)
		{
			primitive.MidiEvent (Channel, 0xC0, program, 0);
		}

		public void MidiCAf (byte velocity)
		{
			primitive.MidiEvent (Channel, 0xD0, velocity, 0);
		}

		public void MidiPitch (int value)
		{
			primitive.MidiEvent (Channel, 0xE0, (byte)(value % 0x80), (byte)(value / 0x80));
		}

		public void MidiMeta (int metaType, params byte [] bytes)
		{
			primitive.MidiMeta (metaType, bytes);
		}

		public void MidiSysex (byte [] bytes, int offset, int length)
		{
			primitive.MidiSysex (bytes, offset, length);
		}

		public void MidiSysex (params byte [] bytes)
		{
			primitive.MidiSysex (bytes, 0, bytes.Length);
		}

		public void MidiMeta (int metaType, string data)
		{
			primitive.MidiMeta (metaType, data);
		}

		#endregion // MIDI operators

		[Macro ("@")]
		public void ProgramWithBank (byte program, byte bankMsb, byte bankLsb)
		{
			MidiCC (0, bankMsb);
			MidiCC (0x20, bankLsb);
			MidiProgramChange (program);
		}

		#region Spectral changes

		public class Spectra
		{
			public Spectra (TrackController controller, Action<int> onSetValue)
			{
				this.controller = controller;
				this.on_set_value = onSetValue;
			}

			TrackController controller;
			Action<int> on_set_value;

			int preserved;
			public int Value {
				get => preserved;
				set {
					preserved = value;
					on_set_value (value);
				}
			}

			[MacroSuffix ("_")]
			public void OneShot (int startValue, int endValue, Length startDelay, Length length, int deltaLength = 4)
			{
				int initialTimelinePosition = controller.TimelinePosition;
				int workRepeatTime = length / deltaLength;
				controller.TimelinePosition += startDelay;
				Value = startValue;
				for (int i = 0; i < workRepeatTime; i++) {
					controller.TimelinePosition += deltaLength;
					Value += (endValue - startValue) / (i + 1);
				}
				Value = endValue;
				controller.TimelinePosition += initialTimelinePosition;
			}

			[MacroSuffix ("t")]
			public void Triangle (int startValue, int endValue, Length startDelay, Length endDuration, int ts, int es, int delta, int repeats)
			{
				int initialTimelinePosition = controller.TimelinePosition;
				controller.TimelinePosition += startDelay;
				Value = startValue;
				for (int r = 0; r < repeats; r++) {
					for (int i = 0; i < ts / es; i++) {
						controller.TimelinePosition += es;
						Value += delta;
					}
					for (int i = 0; i < ts / es; i++) {
						controller.TimelinePosition += es;
						Value -= delta;
					}
				}
				controller.TimelinePosition += endDuration;
				Value = endValue;
				controller.TimelinePosition += initialTimelinePosition;
			}
		}

		int tempo;

		[Macro ("TEMPO")]
		public int TempoValue {
			get => tempo;
			set {
				tempo = value;
				MidiMeta (0x51, (byte)(value / 0x10000), (byte)(value % 0x10000 / 0x100), (byte)(value % 0x100));
			}
		}

		[MacroRelative ("t", "t+", "t-")]
		public Spectra Tempo { get; private set; }

		int pitchbend_cent;

		[Macro ("BEND_CENT_MODE")]
		public int PitchBendRatioByKeys { get; set; }

		[Macro ("BEND")]
		public int PitchBendValue {
			get => pitchbend_cent;
			set {
				pitchbend_cent = value;
				MidiPitch (PitchBendRatioByKeys != 0 ? value / 100 * 8192 / PitchBendRatioByKeys : value);
			}
		}

		// FIXME: is it in the right order?
		[Macro ("PITCH_BEND_SENSITIVITY")]
		public void PitchBendSensitivity (byte value)
		{
			Rpn (0, 0);
			Dte (value, 0);
		}

		[MacroRelative ("B", "B+", "B-")]
		public Spectra PitchBend { get; private set; }
		[MacroRelative ("E", "E+", "E-")]
		public Spectra Expression { get; private set; }
		[MacroRelative ("M", "M+", "M-")]
		public Spectra Modulation { get; private set; }
		[MacroRelative ("V", "V+", "V-")]
		public Spectra Volume { get; private set; }
		[MacroRelative ("P", "P+", "P-")]
		public Spectra Pan { get; private set; }
		[MacroRelative ("H", "H+", "H-")]
		public Spectra DumperPedal { get; private set; }

		[Macro ("DTEM")]
		public void DteMsb (byte value) => MidiCC (6, value);

		[Macro ("DTEL")]
		public void DteLsb (byte value) => MidiCC (0x26, value);

		[Macro ("DTE")]
		public void Dte (byte msb, byte lsb)
		{
			DteMsb (msb);
			DteLsb (lsb);
		}

		[Macro ("SOS")]
		public void Sostenuto (byte value) => MidiCC (0x42, value);

		[Macro ("SOFT")]
		public void SoftPedal (byte value) => MidiCC (0x43, value);

		[Macro ("LEGATO")]
		public void Legato (byte value) => MidiCC (0x54, value);

		[MacroRelative ("RSD", "RSD+", "RSD-")]
		public Spectra ReverbSendDepth { get; private set; }

		[MacroRelative ("CSD", "CSD+", "CSD-")]
		public Spectra ChorusSendDepth { get; private set; }

		[MacroRelative ("DSD", "DSD+", "DSD-")]
		public Spectra DelaySendDepth { get; private set; }

		[Macro ("NRPNM")]
		public void NrpnMSb (byte value) => MidiCC (0x63, value);

		[Macro ("NRPNL")]
		public void NrpnLSb (byte value) => MidiCC (0x62, value);

		[Macro ("NRPN")]
		public void Nrpn (byte msb, byte lsb)
		{
			NrpnMSb (msb);
			NrpnLSb (lsb);
		}

		[Macro ("RPNM")]
		public void RpnMSb (byte value) => MidiCC (0x65, value);

		[Macro ("RPNL")]
		public void RpnLSb (byte value) => MidiCC (0x64, value);

		[Macro ("RPN")]
		public void Rpn (byte msb, byte lsb)
		{
			RpnMSb (msb);
			RpnLSb (lsb);
		}

		#endregion // Control changes

		#region META events

		public void Text (string value) => MidiMeta (1, value);

		public void Copyright (string value) => MidiMeta (2, value);

		public void TrackName (string value) => MidiMeta (3, value);

		public void InstrumentName (string value) => MidiMeta (4, value);

		public void Lyric (string value) => MidiMeta (5, value);

		public void Marker (string value) => MidiMeta (6, value);

		public void Cue (string value) => MidiMeta (7, value);

		public void Beat (byte value, byte denominator)
		{

			MidiMeta (0x58, value, (byte)
				(denominator == 2 ? 1 :
				denominator == 4 ? 2 :
				denominator == 8 ? 3 :
				denominator == 16 ? 4 :
				denominator),
				0, 0);
		}

		#endregion // META events

		#region Note flavors

		[Macro ("v")]
		public int Velocity { get; set; } = 100;

		public int VelocityRelativeSensitivity { get; set; } = 4;

		[Macro (")")]
		public void IncreateVelocity () => Velocity += VelocityRelativeSensitivity;

		[Macro ("(")]
		public void DecreateVelocity () => Velocity -= VelocityRelativeSensitivity;

		[Macro ("l")]
		public Length DefaultLength { get; set; } = 4;

		[Macro ("TIMING")]
		public int KeyDelay { get; set; }

		[Macro ("GATE_DENOM")]
		public int GateTimeDenominator { get; set; } = 8;
		[Macro ("Q")]
		public int GateTimeRelative { get; set; } = 8;
		[Macro ("q")]
		public int GateTimeAbsolute { get; set; }

		[Macro ("o")]
		public int Octave { get; set; } = 4;

		[Macro (">")]
		public void IncreaseOctave () => Octave++;

		[Macro ("<")]
		public void DecreaseOctave () => Octave--;

		[Macro ("K")]
		public int Transpose { get; set; }
		public int TransposeC { get; set; }
		public int TransposeD { get; set; }
		public int TransposeE { get; set; }
		public int TransposeF { get; set; }
		public int TransposeG { get; set; }
		public int TransposeA { get; set; }
		public int TransposeB { get; set; }

		[Macro ("Kc+")]
		public void TransposeCSharp () => TransposeC = 1;
		[Macro ("Kd+")]
		public void TransposeDSharp () => TransposeD = 1;
		[Macro ("Ke+")]
		public void TransposeESharp () => TransposeE = 1;
		[Macro ("Kf+")]
		public void TransposeFSharp () => TransposeF = 1;
		[Macro ("Kg+")]
		public void TransposeGSharp () => TransposeG = 1;
		[Macro ("Ka+")]
		public void TransposeASharp () => TransposeA = 1;
		[Macro ("Kb+")]
		public void TransposeBSharp () => TransposeB = 1;

		[Macro ("Kc-")]
		public void TransposeCFlat () => TransposeC = -1;
		[Macro ("Kd-")]
		public void TransposeDFlat () => TransposeD = -1;
		[Macro ("Ke-")]
		public void TransposeEFlat () => TransposeE = -1;
		[Macro ("Kf-")]
		public void TransposeFFlat () => TransposeF = -1;
		[Macro ("Kg-")]
		public void TransposeGFlat () => TransposeG = -1;
		[Macro ("Ka-")]
		public void TransposeAFlat () => TransposeA = -1;
		[Macro ("Kb-")]
		public void TransposeBFlat () => TransposeB = -1;

		[Macro ("Kc=")]
		public void TransposeCNatural () => TransposeC = 0;
		[Macro ("Kd=")]
		public void TransposeDNatural () => TransposeD = 0;
		[Macro ("Ke=")]
		public void TransposeENatural () => TransposeE = 0;
		[Macro ("Kf=")]
		public void TransposeFNatural () => TransposeF = 0;
		[Macro ("Kg=")]
		public void TransposeGNatural () => TransposeG = 0;
		[Macro ("Ka=")]
		public void TransposeANatural () => TransposeA = 0;
		[Macro ("Kb=")]
		public void TransposeBNatural () => TransposeB = 0;

		#endregion // Note flavors

		#region Note and rest operators

		[Macro ("n")]
		public void Note (byte key, int step = -1, int gate = -1, int velocity = -1, int keyDelay = -1, byte noteOffVelocity = 0)
		{
			step = step < 0 ? (int)DefaultLength : step;
			velocity = velocity < 0 ? this.Velocity : velocity;
			keyDelay = keyDelay < 0 ? this.KeyDelay : keyDelay;

			int currentNoteStep = gate < 0 ? step : gate;
			int currentNoteGate = (int)(currentNoteStep * GateTimeRelative * (1.0 / GateTimeDenominator)) - GateTimeAbsolute;

			Step (keyDelay);
			MidiNoteOn (key, (byte)velocity);
			Step (currentNoteGate);
			// see SyncNoteOffWithNext()
			// OnMidiNoteOff (currentNoteGate, key, velocity);
			MidiNoteOff (key, noteOffVelocity);
			Step (step - currentNoteGate);
			Rewind (keyDelay);
		}

		/*
		I'm going to remove support for "&" arpeggio support
		because it does not cope with "live" operations.
		Arpeggio can be achieved by some starter marking, not suffixed operator e.g. `ARPceg2.ARPfa>c2.`

		"&" was introduced in mugene because its referent syntax
		MUC had the operator and it was quite useful.
		But it was a language from 20C which never cared live
		operations...

		This will simplify MML processing significantly.

		[Macro ("&")]
		public void SyncNoteOffWithNext ()
		{
			primitive.SyncNoteOffWithNext (channel);
		}
		*/

		public class NoteGroup
		{
			public NoteGroup (TrackController controller, byte baseKey, Func<int> transposeSpecific)
			{
				Base = new NoteOperator (controller, baseKey, transposeSpecific);
				Flat = new NoteOperator (controller, baseKey, () => -1);
				Natural = new NoteOperator (controller, baseKey, () => 0);
				Sharp = new NoteOperator (controller, baseKey, () => 1);
			}

			[MacroSuffix ("")]
			public NoteOperator Base { get; private set; }
			[MacroSuffix ("-")]
			public NoteOperator Flat { get; private set; }
			[MacroSuffix ("=")]
			public NoteOperator Natural { get; private set; }
			[MacroSuffix ("+")]
			public NoteOperator Sharp { get; private set; }
		}

		public class NoteOperator
		{
			public NoteOperator (TrackController controller, byte baseKey, Func<int> transposeSpecific)
			{
				this.controller = controller;
				BaseKey = baseKey;
				TransposeSpecific = transposeSpecific;
			}

			TrackController controller;
			public byte BaseKey { get; set; }
			public Func<int> TransposeSpecific { get; set; }

			public void Note (int step = -1, int gate = -1, int velocity = -1, int keyDelay = -1, byte noteOffVelocity = 0) => controller.Note ((byte)(controller.Octave * 12 + BaseKey + TransposeSpecific () + controller.Transpose), step, gate, velocity, keyDelay, noteOffVelocity);
		}

		[MacroNote ("c")]
		public NoteGroup NoteC { get; private set; }
		[MacroNote ("d")]
		public NoteGroup NoteD { get; private set; }
		[MacroNote ("e")]
		public NoteGroup NoteE { get; private set; }
		[MacroNote ("f")]
		public NoteGroup NoteF { get; private set; }
		[MacroNote ("g")]
		public NoteGroup NoteG { get; private set; }
		[MacroNote ("a")]
		public NoteGroup NoteA { get; private set; }
		[MacroNote ("b")]
		public NoteGroup NoteB { get; private set; }

		[Macro ("r")]
		public void Rest (int step) => Step (step);

		#endregion // Note and rest operators

		#region Loop operators

		// I'm not sure if defining loop in API makes sense, but so far for backward compatibility...

		[Macro ("[")]
		public void BeginLoop () => primitive.BeginLoop (Channel);

		[Macro (":")]
		[Macro ("/")]
		public void BreakLoop (params int [] targets) => primitive.BreakLoop (Channel, targets);

		[Macro ("]")]
		public void EndLoop (int repeats) => primitive.EndLoop (Channel, repeats);

		#endregion // Loop operators

		[Macro ("GM_SYSTEM_ON")]
		public void GMSystemOn () => MidiSysex (0xF0, 0x7E, 0x7F, 0x09, 0x01, 0xF7);
		[Macro ("XG_RESET")]
		public void XGReset () => MidiSysex (0xF0, 0x43, 0x10, 0x4C, 0, 0, 0x7E, 0, 0xF7);

		void InitializeFields ()
		{
			Tempo = new Spectra (this, v => TempoValue = v);

			PitchBend = new Spectra (this, v => PitchBendValue = v);
			Expression = new Spectra (this, v => MidiCC (0x0B, (byte)v));
			Modulation = new Spectra (this, v => MidiCC (1, (byte)v));
			Volume = new Spectra (this, v => MidiCC (7, (byte)v));
			Pan = new Spectra (this, v => MidiCC (0x0A, (byte)v));
			DumperPedal = new Spectra (this, v => MidiCC (0x40, (byte)v));

			ReverbSendDepth = new Spectra (this, v => MidiCC (0x5B, (byte)v));
			ChorusSendDepth = new Spectra (this, v => MidiCC (0x5D, (byte)v));
			DelaySendDepth = new Spectra (this, v => MidiCC (0x5E, (byte)v));

			NoteC = new NoteGroup (this, 0, () => TransposeC);
			NoteD = new NoteGroup (this, 2, () => TransposeD);
			NoteE = new NoteGroup (this, 4, () => TransposeE);
			NoteF = new NoteGroup (this, 5, () => TransposeF);
			NoteG = new NoteGroup (this, 7, () => TransposeG);
			NoteA = new NoteGroup (this, 9, () => TransposeA);
			NoteB = new NoteGroup (this, 11, () => TransposeB);
		}
	}
}
