using System.Numerics;

namespace PlotDrift;

public static class CalculateDelay
{
    public static double UsingCrossCorrelation(double[] channel1, double[] channel2, double sampleRate, int fftLength)
    {
        // Ensure the length of both channels is a power of 2
        var fft1 = FFT(channel1, fftLength);
        var fft2 = FFT(channel2, fftLength);

        // Conjugate and multiply
        var crossCorrelation = new Complex[fftLength];
        for (int index = 0; index < fftLength; index++)
        {
            crossCorrelation[index] = fft1[index] * Complex.Conjugate(fft2[index]);
        }

        // Inverse FFT to get the cross-correlation series
        FftSharp.FFT.Inverse(crossCorrelation);

        // Find the index of the maximum value in cross-correlation
        var maxIndex = Array.IndexOf(crossCorrelation.Select(x => x.Magnitude).ToArray(), crossCorrelation.Max(x => x.Magnitude));

        // Convert index to time delay (consider FFT shift for correct direction)
        var shift = maxIndex > fftLength / 2 ? maxIndex - fftLength : maxIndex;
        return -shift / sampleRate;
    }

    private static Complex[] FFT(double[] data, int fftLength)
    {
        // Extend or truncate the array to the desired length, filled with zeros if needed
        var complexData = new Complex[fftLength];
        for (int index = 0; index < fftLength; index++)
        {
            if (index < data.Length)
            {
                complexData[index] = new Complex(data[index], 0);
            }
            else
            {
                complexData[index] = new Complex(0, 0);
            }
        }

        // Perform the FFT operation using FftSharp
        FftSharp.FFT.Forward(complexData);
        return complexData;
    }
}
