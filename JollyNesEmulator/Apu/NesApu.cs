using JollyNesEmulator.Nes;

namespace JollyNesEmulator.Apu
{
    public sealed class NesApu
    {
        private const double CpuFrequency = 1789773.0;
        private const int SampleRate = 44100;

        private readonly NesAudio _audio;

        private double _sampleAccumulator;

        private readonly PulseChannel _pulse1 = new PulseChannel();
        private readonly PulseChannel _pulse2 = new PulseChannel();
        private readonly TriangleChannel _triangle = new TriangleChannel();
        private readonly NoiseChannel _noise = new NoiseChannel();

        private double _previousAudioInput;
        private double _previousAudioOutput;
        
        private int _frameSequenceCycles;
        private bool _frameSequenceMode5Step;
        private bool _frameInterruptInhibit;
        private bool _frameInterruptFlag;

        private byte _status;
        private byte _frameCounter;

        private bool _apuCycle;

        public NesApu(NesAudio audio)
        {
            _audio = audio;
        }

        public byte ReadRegister(ushort address)
        {
            if (address == 0x4015)
                return ReadStatus();

            return 0;
        }

        public void WriteRegister(ushort address, byte value)
        {
            switch (address)
            {
                case 0x4000:
                    _pulse1.WriteControl(value);
                    break;

                case 0x4001:
                    _pulse1.WriteSweep(value);
                    break;

                case 0x4002:
                    _pulse1.WriteTimerLow(value);
                    break;

                case 0x4003:
                    _pulse1.WriteTimerHigh(value);
                    break;

                case 0x4004:
                    _pulse2.WriteControl(value);
                    break;

                case 0x4005:
                    _pulse2.WriteSweep(value);
                    break;

                case 0x4006:
                    _pulse2.WriteTimerLow(value);
                    break;

                case 0x4007:
                    _pulse2.WriteTimerHigh(value);
                    break;

                case 0x4008:
                    _triangle.WriteControl(value);
                    break;

                case 0x4009:
                    break;

                case 0x400A:
                    _triangle.WriteTimerLow(value);
                    break;

                case 0x400B:
                    _triangle.WriteTimerHigh(value);
                    break;

                case 0x400C:
                    _noise.WriteControl(value);
                    break;

                case 0x400D:
                    break;

                case 0x400E:
                    _noise.WritePeriod(value);
                    break;

                case 0x400F:
                    _noise.WriteLength(value);
                    break;

                case 0x4015:
                    WriteStatus(value);
                    break;

                case 0x4017:
                    WriteFrameCounter(value);
                    break;
            }
        }

        private void WriteFrameCounter(byte value)
        {
            _frameCounter = value;
            _frameSequenceMode5Step = (value & 0x80) != 0;
            _frameInterruptInhibit = (value & 0x40) != 0;

            if (_frameInterruptInhibit)
                _frameInterruptFlag = false;

            _frameSequenceCycles = 0;

            if (_frameSequenceMode5Step)
            {
                ClockQuarterFrame();
                ClockHalfFrame();
            }
        }

        public void Step(int cpuCycles)
        {
            if (cpuCycles <= 0)
                return;

            for (int index = 0; index < cpuCycles; index++)
            {
                ClockChannels();
                StepFrameSequencer();

                _sampleAccumulator += SampleRate / CpuFrequency;

                if (_sampleAccumulator >= 1.0)
                {
                    _sampleAccumulator -= 1.0;
                    GenerateSample();
                }
            }

            _audio.Flush();
        }

        private void StepFrameSequencer()
        {
            _frameSequenceCycles++;

            if (!_frameSequenceMode5Step)
            {
                if (_frameSequenceCycles == 3729)
                {
                    ClockQuarterFrame();
                }
                else if (_frameSequenceCycles == 7457)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();
                }
                else if (_frameSequenceCycles == 11186)
                {
                    ClockQuarterFrame();
                }
                else if (_frameSequenceCycles == 14915)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();

                    if (!_frameInterruptInhibit)
                        _frameInterruptFlag = true;

                    _frameSequenceCycles = 0;
                }
            }
            else
            {
                if (_frameSequenceCycles == 3729)
                {
                    ClockQuarterFrame();
                }
                else if (_frameSequenceCycles == 7457)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();
                }
                else if (_frameSequenceCycles == 11186)
                {
                    ClockQuarterFrame();
                }
                else if (_frameSequenceCycles == 14915)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();
                }
                else if (_frameSequenceCycles == 18641)
                {
                    ClockQuarterFrame();
                    ClockHalfFrame();
                    _frameSequenceCycles = 0;
                }
            }
        }

        private void ClockQuarterFrame()
        {
            _pulse1.ClockEnvelope();
            _pulse2.ClockEnvelope();
            _triangle.ClockLinearCounter();
            _noise.ClockEnvelope();
        }

        private void ClockHalfFrame()
        {
            _pulse1.ClockLengthCounter();
            _pulse2.ClockLengthCounter();
            _triangle.ClockLengthCounter();
            _noise.ClockLengthCounter();

            _pulse1.ClockSweep();
            _pulse2.ClockSweep();
        }

        public void Reset()
        {
            _sampleAccumulator = 0;
            _frameSequenceCycles = 0;
            _frameSequenceMode5Step = false;
            _frameInterruptInhibit = false;
            _frameInterruptFlag = false;
            _status = 0;
            _frameCounter = 0;
            _apuCycle = false;
            _previousAudioInput = 0;
            _previousAudioOutput = 0;

            _pulse1.Reset();
            _pulse2.Reset();
            _triangle.Reset();
            _noise.Reset();

            _audio.Reset();
        }

        private void ClockChannels()
        {
            _triangle.Step();

            _apuCycle = !_apuCycle;

            if (_apuCycle)
                return;

            _pulse1.Step();
            _pulse2.Step();
            _noise.Step();
        }

        private void GenerateSample()
        {
            double pulse1 = _pulse1.Output();
            double pulse2 = _pulse2.Output();
            double triangle = _triangle.Output();
            double noise = _noise.Output();

            double pulseSum = pulse1 + pulse2;
            double pulseOutput = pulseSum > 0.0 ? 95.88 / ((8128.0 / pulseSum) + 100.0) : 0.0;

            double tndInput = (triangle / 8227.0) + (noise / 12241.0);
            double tndOutput = tndInput > 0.0 ? 159.79 / ((1.0 / tndInput) + 100.0) : 0.0;

            double sample = Math.Clamp(pulseOutput + tndOutput, 0.0, 1.0);
            double input = (sample * 2.0) - 1.0;

            const double filterCoefficient = 0.995;

            double output = input - _previousAudioInput + (filterCoefficient * _previousAudioOutput);

            _previousAudioInput = input;
            _previousAudioOutput = output;

            output = Math.Clamp(output, -1.0, 1.0);

            short pcm = (short)(output * 32767.0);

            _audio.AddSample(pcm);
        }

        private byte ReadStatus()
        {
            byte status = 0;

            if (_pulse1.LengthCounter > 0)
                status |= 0x01;

            if (_pulse2.LengthCounter > 0)
                status |= 0x02;

            if (_triangle.LengthCounter > 0)
                status |= 0x04;

            if (_noise.LengthCounter > 0)
                status |= 0x08;

            return status;
        }

        private void WriteStatus(byte value)
        {
            _status = value;

            if ((value & 0x01) == 0)
                _pulse1.LengthCounter = 0;

            if ((value & 0x02) == 0)
                _pulse2.LengthCounter = 0;

            if ((value & 0x04) == 0)
                _triangle.LengthCounter = 0;

            if ((value & 0x08) == 0)
                _noise.LengthCounter = 0;
        }

        private sealed class PulseChannel
        {
            private static readonly int[][] DutyTable =
            {
                new[] { 0, 1, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 1, 1, 0, 0, 0, 0, 0 },
                new[] { 0, 1, 1, 1, 1, 0, 0, 0 },
                new[] { 1, 0, 0, 1, 1, 1, 1, 1 }
            };

            private int _duty;
            private int _volume;
            private int _timer;
            private int _timerCounter;
            private int _sequence;
            private bool _constantVolume;
            private bool _envelopeLoop;
            private bool _envelopeStart;
            private int _envelopeDivider;
            private int _envelopeDecay;
            private bool _sweepEnabled;
            private int _sweepPeriod;
            private bool _sweepNegate;
            private int _sweepShift;
            private int _sweepDivider;
            private bool _sweepReload;

            public int LengthCounter { get; set; }

            public void ClockEnvelope()
            {
                if (_envelopeStart)
                {
                    _envelopeStart = false;
                    _envelopeDecay = 15;
                    _envelopeDivider = _volume;
                    return;
                }

                if (_envelopeDivider > 0)
                {
                    _envelopeDivider--;
                    return;
                }

                _envelopeDivider = _volume;

                if (_envelopeDecay > 0)
                {
                    _envelopeDecay--;
                }
                else if (_envelopeLoop)
                {
                    _envelopeDecay = 15;
                }
            }

            public void ClockLengthCounter()
            {
                if (_envelopeLoop)
                    return;

                if (LengthCounter > 0)
                    LengthCounter--;
            }

            public void WriteControl(byte value)
            {
                _duty = (value >> 6) & 0x03;
                _constantVolume = (value & 0x10) != 0;
                _volume = value & 0x0F;
            }

            public void WriteSweep(byte value)
            {
                _sweepEnabled = (value & 0x80) != 0;
                _sweepPeriod = ((value >> 4) & 0x07) + 1;
                _sweepNegate = (value & 0x08) != 0;
                _sweepShift = value & 0x07;
                _sweepReload = true;
            }

            public void ClockSweep()
            {
                bool reload = _sweepReload;
                _sweepReload = false;

                if (_sweepDivider > 0)
                {
                    _sweepDivider--;
                }
                else
                {
                    _sweepDivider = _sweepPeriod;

                    if (_sweepEnabled && _sweepShift > 0)
                        ApplySweep();
                }

                if (reload)
                    _sweepDivider = _sweepPeriod;
            }

            private void ApplySweep()
            {
                int change = _timer >> _sweepShift;
                int targetTimer = _sweepNegate ? _timer - change : _timer + change;

                if (targetTimer < 0 || targetTimer > 0x7FF)
                    return;

                _timer = targetTimer;
            }

            public void WriteTimerLow(byte value)
            {
                _timer = (_timer & 0x700) | value;
            }

            public void WriteTimerHigh(byte value)
            {
                _timer = (_timer & 0x0FF) | ((value & 0x07) << 8);
                LengthCounter = LengthTable[(value >> 3) & 0x1F];
                _sequence = 0;
                _timerCounter = _timer;
            }

            public void Step()
            {
                if (LengthCounter <= 0)
                    return;

                if (_timerCounter <= 0)
                {
                    _timerCounter = _timer;
                    _sequence = (_sequence + 1) & 7;
                }
                else
                {
                    _timerCounter--;
                }
            }

            public double Output()
            {
                if (LengthCounter <= 0)
                    return 0;

                if (_timer < 8)
                    return 0;

                if (DutyTable[_duty][_sequence] == 0)
                    return 0;

                return _constantVolume ? _volume : _envelopeDecay;
            }

            public void Reset()
            {
                _duty = 0;
                _volume = 0;
                _timer = 0;
                _timerCounter = 0;
                _sequence = 0;
                _constantVolume = false;
                _envelopeLoop = false;
                _envelopeStart = false;
                _envelopeDivider = 0;
                _envelopeDecay = 0;
                _sweepEnabled = false;
                _sweepPeriod = 0;
                _sweepNegate = false;
                _sweepShift = 0;
                _sweepDivider = 0;
                _sweepReload = false;
                LengthCounter = 0;
            }
        }

        private sealed class TriangleChannel
        {
            private static readonly int[] Wave =
            {
                15, 14, 13, 12, 11, 10, 9, 8,
                7, 6, 5, 4, 3, 2, 1, 0,
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15
            };

            private int _timer;
            private int _timerCounter;
            private int _sequence;
            private bool _control;
            private int _linearCounter;
            private int _linearReloadValue;
            private bool _linearReload;

            public int LengthCounter { get; set; }

            public void ClockLinearCounter()
            {
                if (_linearReload)
                {
                    _linearCounter = _linearReloadValue;
                }
                else if (_linearCounter > 0)
                {
                    _linearCounter--;
                }

                if (!_control)
                    _linearReload = false;
            }

            public void ClockLengthCounter()
            {
                if (_control)
                    return;

                if (LengthCounter > 0)
                    LengthCounter--;
            }

            public void WriteControl(byte value)
            {
                _control = (value & 0x80) != 0;
                _linearReloadValue = value & 0x7F;
            }

            public void WriteTimerLow(byte value)
            {
                _timer = (_timer & 0x700) | value;
            }

            public void WriteTimerHigh(byte value)
            {
                _timer = (_timer & 0x0FF) | ((value & 0x07) << 8);
                LengthCounter = LengthTable[(value >> 3) & 0x1F];
                _sequence = 0;
                _timerCounter = _timer;
                _linearReload = true;
            }

            public void Step()
            {
                if (LengthCounter <= 0)
                    return;

                if (_linearCounter <= 0)
                    return;

                if (_timerCounter <= 0)
                {
                    _timerCounter = _timer;
                    _sequence = (_sequence + 1) & 31;
                }
                else
                {
                    _timerCounter--;
                }
            }

            public double Output()
            {
                if (LengthCounter <= 0)
                    return 0;

                if (_timer < 2)
                    return 0;

                return Wave[_sequence];
            }

            public void Reset()
            {
                _timer = 0;
                _timerCounter = 0;
                _sequence = 0;
                _control = false;
                _linearCounter = 0;
                _linearReloadValue = 0;
                _linearReload = false;
                LengthCounter = 0;
            }
        }

        private sealed class NoiseChannel
        {
            private static readonly int[] PeriodTable =
            {
                4, 8, 16, 32, 64, 96, 128, 160,
                202, 254, 380, 508, 762, 1016, 2034, 4068
            };

            private ushort _shiftRegister = 1;
            private int _timer;
            private int _timerCounter;
            private int _volume;
            private bool _mode;
            private bool _envelopeLoop;
            private bool _constantVolume;
            private bool _envelopeStart;
            private int _envelopeDivider;
            private int _envelopeDecay;

            public int LengthCounter { get; set; }

            public void ClockEnvelope()
            {
                if (_envelopeStart)
                {
                    _envelopeStart = false;
                    _envelopeDecay = 15;
                    _envelopeDivider = _volume;
                    return;
                }

                if (_envelopeDivider > 0)
                {
                    _envelopeDivider--;
                    return;
                }

                _envelopeDivider = _volume;

                if (_envelopeDecay > 0)
                {
                    _envelopeDecay--;
                }
                else if (_envelopeLoop)
                {
                    _envelopeDecay = 15;
                }
            }

            public void ClockLengthCounter()
            {
                if (_envelopeLoop)
                    return;

                if (LengthCounter > 0)
                    LengthCounter--;
            }

            public void WriteControl(byte value)
            {
                _envelopeLoop = (value & 0x20) != 0;
                _constantVolume = (value & 0x10) != 0;
                _volume = value & 0x0F;
                _envelopeStart = true;
            }

            public void WritePeriod(byte value)
            {
                _mode = (value & 0x80) != 0;
                _timer = PeriodTable[value & 0x0F];
            }

            public void WriteLength(byte value)
            {
                LengthCounter = LengthTable[(value >> 3) & 0x1F];
            }

            public void Step()
            {
                if (LengthCounter <= 0)
                    return;

                if (_timerCounter <= 0)
                {
                    _timerCounter = _timer;

                    int tap = _mode ? 6 : 1;
                    int feedback = ((_shiftRegister & 1) ^ ((_shiftRegister >> tap) & 1));
                    _shiftRegister >>= 1;
                    _shiftRegister |= (ushort)(feedback << 14);
                }
                else
                {
                    _timerCounter--;
                }
            }

            public double Output()
            {
                if (LengthCounter <= 0)
                    return 0;

                if ((_shiftRegister & 1) != 0)
                    return 0;

                return _constantVolume ? _volume : _envelopeDecay;
            }

            public void Reset()
            {
                _shiftRegister = 1;
                _timer = 0;
                _timerCounter = 0;
                _volume = 0;
                _mode = false;
                _envelopeLoop = false;
                _constantVolume = false;
                _envelopeStart = false;
                _envelopeDivider = 0;
                _envelopeDecay = 0;
                LengthCounter = 0;
            }
        }

        private static readonly int[] LengthTable =
        {
            10, 254, 20, 2, 40, 4, 80, 6,
            160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22,
            192, 24, 72, 26, 16, 28, 32, 30
        };
    }
}
