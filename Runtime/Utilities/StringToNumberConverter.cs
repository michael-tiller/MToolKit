using System;
using System.Security.Cryptography;
using System.Text;

namespace MToolKit.Runtime.Utilities
{
  public static class StringToNumberConverter
  {
    public static int ConvertStringToNumber(string input)
    {
      // Use SHA256 to hash the string
      using (SHA256 sha256Hash = SHA256.Create())
      {
        // Convert the input string to a byte array and compute the hash
        byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Convert the first 4 bytes of the hash to an integer
        int hashValue = BitConverter.ToInt32(data, 0);

        // Ensure the number is non-negative
        hashValue = Math.Abs(hashValue);

        // Scale the number to be between 0 and 100,000,000
        return hashValue % 100000001; // Use modulo to wrap around at 100,000,001
      }
    }
  }
}