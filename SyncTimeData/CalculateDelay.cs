using System.Numerics;

namespace SyncTimeData;

public static class CalculateDelay
{
    public static int UsingCrossCorrelation(IEnumerable<double> channel1, IEnumerable<double> channel2, int length)
    {
        var fftLength = NextPowerOf2(length * 2);

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
        return -shift;
    }

    private static Complex[] FFT(IEnumerable<double> data, int fftLength)
    {
        // Extend or truncate the array to the desired length, filled with zeros if needed
        var complexData = new Complex[fftLength];
        for (int index = 0; index < fftLength; index++)
        {
            if (index < fftLength / 2)
            {
                complexData[index] = new Complex(data.ElementAt(index), 0);
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

    public static int NextPowerOf2(int n)
    {
        if (n < 1)
        {
            throw new ArgumentException("Number must be positive", nameof(n));
        }

        // If the number is already a power of 2, return it
        if ((n & (n - 1)) == 0)
        {
            return n;
        }

        // Subtract 1, to handle cases where `n` itself is a power of 2
        n--;

        // Set all bits after the last set bit
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;

        // Adding 1 to the modified number gives us the next power of 2
        return n + 1;
    }
}
