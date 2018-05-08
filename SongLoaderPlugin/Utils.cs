using System;
using System.Text;

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
		
		public static string CreateMD5(string input)
		{
			// Use input string to calculate MD5 hash
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
				byte[] hashBytes = md5.ComputeHash(inputBytes);

				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
			}
		}
	}
}