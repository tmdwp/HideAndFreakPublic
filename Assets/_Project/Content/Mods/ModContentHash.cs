using System;
using System.IO;
using System.Security.Cryptography;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 파일이 손상·교체됐는지 찾도록 번들 내용의 지문을 계산한다.</summary>
    public static class ModContentHash
    {
        private const int BufferSize = 1024 * 1024;
        private const int HashByteCount = 8;

        /// <summary>수백 MB 번들도 메모리에 다 올리지 않도록 나눠 읽으며 해시한다.</summary>
        /// <exception cref="IOException">파일을 읽을 수 없을 때.</exception>
        /// <exception cref="UnauthorizedAccessException">파일 접근 권한이 없을 때.</exception>
        public static string Compute(string folder, params string[] fileNames)
        {
            using SHA256 sha = SHA256.Create();
            var buffer = new byte[BufferSize];
            foreach (string fileName in fileNames)
            {
                if (string.IsNullOrEmpty(fileName))
                    continue;

                using FileStream stream = File.OpenRead(Path.Combine(folder, fileName));
                int readCount;
                while ((readCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                    sha.TransformBlock(buffer, 0, readCount, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha.Hash, 0, HashByteCount).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
