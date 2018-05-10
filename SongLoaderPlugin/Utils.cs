using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace SongLoaderPlugin
{
	public static class Utils
	{
		public static TEnum ToEnum<TEnum>(this string strEnumValue, TEnum defaultValue)
		{
			if (!Enum.IsDefined(typeof(TEnum), strEnumValue))
				return defaultValue;

			return (TEnum)Enum.Parse(typeof(TEnum), strEnumValue);
		}
		
		public static string CreateMD5FromString(string input)
		{
			// Use input string to calculate MD5 hash
			using (var md5 = MD5.Create())
			{
				var inputBytes = Encoding.ASCII.GetBytes(input);
				var hashBytes = md5.ComputeHash(inputBytes);

				// Convert the byte array to hexadecimal string
				var sb = new StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
			}
		}
		
		public static bool CreateMD5FromFile(string path, out string hash)
		{
			hash = "";
			if (!File.Exists(path)) return false;
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(path))
				{
					var hashBytes = md5.ComputeHash(stream);

					// Convert the byte array to hexadecimal string
					var sb = new StringBuilder();
					foreach (var hashByte in hashBytes)
					{
						sb.Append(hashByte.ToString("X2"));
					}

					hash = sb.ToString();
					return true;
				}
			}
		}
	}
}