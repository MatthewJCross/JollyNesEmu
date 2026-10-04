using Microsoft.VisualBasic.Devices;
using NAudio.Wave;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace JollyNesEmulator.Nes
{
    public sealed class NesAudio : IDisposable
    {
        private const int SampleRate = 44100;
        private const int BufferSamples = 1024;

        private readonly BufferedWaveProvider _buffer;
        private readonly WaveOutEvent _output;
        private readonly short[] _sampleBuffer = new short[BufferSamples];
        private int _sampleBufferPosition;
        private double _phase;

        public NesAudio()
        {
            WaveFormat format = new WaveFormat(SampleRate, 16, 1);
            _buffer = new BufferedWaveProvider(format);
            _buffer.DiscardOnBufferOverflow = true;
            _output = new WaveOutEvent();
            _output.Init(_buffer);
            _output.Play();
        }

        public void AddSample(short sample)
        {
            _sampleBuffer[_sampleBufferPosition++] = sample;

            if (_sampleBufferPosition >= _sampleBuffer.Length)
                FlushSamples();
        }

        public void AddSamples(short[] samples)
        {
            if (samples.Length == 0)
                return;

            int sourceIndex = 0;

            while (sourceIndex < samples.Length)
            {
                int remaining = samples.Length - sourceIndex;
                int available = _sampleBuffer.Length - _sampleBufferPosition;
                int count = Math.Min(remaining, available);

                Array.Copy(samples, sourceIndex, _sampleBuffer, _sampleBufferPosition, count);

                _sampleBufferPosition += count;
                sourceIndex += count;

                if (_sampleBufferPosition >= _sampleBuffer.Length)
                    FlushSamples();
            }
        }

        public void Flush()
        {
            if (_sampleBufferPosition <= 0)
                return;

            FlushSamples();
        }

        private void FlushSamples()
        {
            int byteCount = _sampleBufferPosition * sizeof(short);
            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(_sampleBuffer, 0, data, 0, byteCount);
            _buffer.AddSamples(data, 0, data.Length);
            _sampleBufferPosition = 0;
        }

        public void Reset()
        {
            _sampleBufferPosition = 0;
            Array.Clear(_sampleBuffer, 0, _sampleBuffer.Length);
            _buffer.ClearBuffer();
            _phase = 0.0;
        }

        public void TestTone()
        {
            const int frequency = 440;
            const int sampleCount = SampleRate;
            short[] samples = new short[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                double value = Math.Sin(_phase) * 0.2;
                samples[index] = (short)(value * short.MaxValue);
                _phase += 2.0 * Math.PI * frequency / SampleRate;

                if (_phase >= Math.PI * 2.0)
                    _phase -= Math.PI * 2.0;
            }

            AddSamples(samples);
        }

        public void Dispose()
        {
            Flush();
            _output.Stop();
            _output.Dispose();
        }
    }
}
