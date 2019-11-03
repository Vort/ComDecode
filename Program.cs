using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ComDecode
{
    class Program
    {
        const int sampleRate = 192000;

        Program()
        {
            double baudRate = 115200;
            double baudRateCorrection = 1.00174;

            short[] srcSignal = LoadWav("signal.wav");

            var bits = new List<bool>();
            var bytes = new List<byte>();

            //  19200:  831..2101
            // 115200: 1785..2620
            int detect_level = 1940;

            int chunkStart = 0;
            int peakIndex = 0;
            short peakValue = 0;

            int bitsReceived = 0;
            int bitErrors = 0;

            var recoverFilter = new double[] { -2.03324, 3.03324 };
            var recoveredSignal = Convolution(recoverFilter, srcSignal);

            var deltaSignal = new short[recoveredSignal.Length - 1];
            for (int i = 0; i < recoveredSignal.Length - 1; i++)
                deltaSignal[i] = (short)(recoveredSignal[i + 1] - recoveredSignal[i]);

            int oversampleFactor = 4;
            deltaSignal = Interpolation(deltaSignal, oversampleFactor);

            int resampleFilterSize = oversampleFactor * 8 + 1;
            deltaSignal = Convolution(MakeLowPassFilter(
                sampleRate / 2, resampleFilterSize, sampleRate * oversampleFactor), deltaSignal);

            double bitLength = sampleRate * oversampleFactor / baudRate / baudRateCorrection;
            for (int i = 0; i < deltaSignal.Length; i++)
            {
                short deltaSample = deltaSignal[i];
                short deltaAbsSample = Math.Abs(deltaSample);
                if ((peakIndex == 0 && deltaAbsSample > detect_level) ||
                    (peakIndex != 0 && deltaAbsSample >= peakValue))
                {
                    peakIndex = i;
                    peakValue = deltaAbsSample;
                }
                else if (peakIndex != 0 && deltaAbsSample < detect_level ||
                    i == deltaSignal.Length - 1)
                {
                    if (i != deltaSignal.Length - 1)
                    {
                        int bitCount = (int)Math.Round((peakIndex - chunkStart) / bitLength);
                        if (bitCount > 9)
                            bitCount = 9;
                        bool bitsValue = deltaSample > 0;
                        for (int x = 0; x < bitCount; x++)
                            bits.Add(bitsValue);
                        bitsReceived += bitCount;
                        while (bits.Count != 0 && bits[0])
                            bits.RemoveAt(0);
                    }
                    else
                        bits.Add(true);

                    if (bits.Count > 9)
                    {
                        int value = 0;
                        for (int x = 0; x < 8; x++)
                        {
                            value >>= 1;
                            value |= bits[x + 1] ? 0x80 : 0;
                        }
                        if (!bits[9])
                            bitErrors++;
                        bytes.Add((byte)value);
                        bits.RemoveRange(0, 10);
                    }
                    chunkStart = peakIndex;
                    peakIndex = 0;
                }
            }

            Console.WriteLine($"Bits received: {bitsReceived}");
            Console.WriteLine($"Bit errors   : {bitErrors}");

            var bytesArray = bytes.ToArray();
            if (bytesArray.Length == 4095 &&
                MD5.Create().ComputeHash(bytesArray).SequenceEqual(
                    new byte[] { 0xc1, 0x8f, 0x62, 0xf1, 0x40, 0x33, 0x13,
                        0x8d, 0x7b, 0xfd, 0xdd, 0x80, 0xe0, 0x45, 0x52, 0x02 }))
            {
                var cc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PRBS15 is found!");
                Console.ForegroundColor = cc;
            }
            else
                File.WriteAllBytes("decoded.txt", bytesArray);
        }

        static void Main(string[] args)
        {
            new Program();
        }

        double[] MakeLowPassFilter(double CutoffFreq, int SampleCount, int sampleRate)
        {
            double W = 2 * Math.PI * (CutoffFreq / sampleRate);
            double A = W / Math.PI;

            int N = SampleCount / 2;

            double[] IR = new double[SampleCount];

            for (int i = 0; i < N + 1; i++)
            {
                if (i == 0)
                    IR[N] = A;
                else
                {
                    double V = A * (Math.Sin(i * W) / (i * W));
                    IR[N + i] = V;
                    IR[N - i] = V;
                }
            }
            return IR;
        }

        double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        short[] Interpolation(short[] signal, int factor)
        {
            if (factor <= 0)
                throw new Exception();
            if (factor == 1)
                return signal;
            if (signal.Length == 0)
                return signal;

            short[] result = new short[(signal.Length - 1) * factor + 1];
            for (int i = 0; i < signal.Length - 1; i++)
            {
                for (int j = 0; j < factor; j++)
                {
                    double t = (double)j / factor;
                    result[i * factor + j] = (short)Lerp(signal[i], signal[i + 1], t);
                }
            }
            result[(signal.Length - 1) * factor] = signal[signal.Length - 1];
            return result;
        }

        short[] Convolution(double[] kernel, short[] signal)
        {
            if (signal.Length <= kernel.Length)
                return new short[0];

            short[] r = new short[signal.Length - kernel.Length];

            int blockSize = 512;
            int blockCount = (r.Length + blockSize - 1) / blockSize;

            Parallel.For(0, blockCount, i =>
            {
                int startSample = i * blockSize;
                int xendSample = blockSize;
                if (i == blockCount - 1)
                    xendSample = r.Length - blockSize * i;
                xendSample += startSample;
                for (int j = startSample; j < xendSample; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < kernel.Length; k++)
                        sum += kernel[k] * signal[j + k];
                    r[j] = (short)sum;
                }
            });
            return r;
        }

        short[] LoadWav(string Path)
        {
            FileStream fs = File.Open(Path, FileMode.Open);
            var br = new BinaryReader(fs);
            br.ReadBytes(12);
            int sampleCount = 0;
            for (;;)
            {
                uint cid = br.ReadUInt32();
                int csize = br.ReadInt32();
                if (cid == 0x61746164)
                {
                    sampleCount = csize / 2;
                    break;
                }
                br.ReadBytes(csize);
            }

            short[] signal = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                signal[i] = br.ReadInt16();
            fs.Close();
            return signal;
        }
    }
}
