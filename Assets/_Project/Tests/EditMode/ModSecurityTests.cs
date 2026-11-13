using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GuardVsMannequin.Map;
using GuardVsMannequin.Mods;
using NUnit.Framework;
using UnityEngine;

namespace GuardVsMannequin.Tests
{
    /// <summary>모드 보안 규칙(컴포넌트 허용 목록, 링크, 번들 지문)을 확인한다.</summary>
    public class ModSecurityTests
    {
        /// <summary>허용 목록이 너무 좁아 정상 맵이 거부되지 않도록, 맵에 쓰는 컴포넌트와 게임 스크립트를 허용하는지 확인한다.</summary>
        [Test]
        public void ComponentPolicy_AllowsMapComponents()
        {
            Assert.IsTrue(ModComponentPolicy.IsAllowed(typeof(Transform)));
            Assert.IsTrue(ModComponentPolicy.IsAllowed(typeof(MeshRenderer)));
            Assert.IsTrue(ModComponentPolicy.IsAllowed(typeof(Light)));
            Assert.IsTrue(ModComponentPolicy.IsAllowed(typeof(MapInfo)));
        }

        /// <summary>모드 맵이 화면·소리를 가로채거나 외부 영상을 재생하지 못하도록, 맵에 필요 없는 컴포넌트를 막는지 확인한다.</summary>
        [Test]
        public void ComponentPolicy_RejectsOtherComponents()
        {
            Assert.IsFalse(ModComponentPolicy.IsAllowed(typeof(Camera)));
            Assert.IsFalse(ModComponentPolicy.IsAllowed(typeof(AudioListener)));
            Assert.IsFalse(ModComponentPolicy.IsAllowed(typeof(UnityEngine.Video.VideoPlayer)));
            Assert.IsFalse(ModComponentPolicy.IsAllowed(null));
        }

        /// <summary>꺼 둔 오브젝트를 나중에 켜는 방식으로 검사를 피하지 못하도록 비활성 자식까지 찾아내는지 확인한다.</summary>
        [Test]
        public void ComponentPolicy_FindsViolationInInactiveChild()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Hidden", typeof(Camera));
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            try
            {
                var violations = new System.Collections.Generic.List<string>();
                ModComponentPolicy.CollectViolations(root, violations);
                Assert.AreEqual(1, violations.Count);
                StringAssert.Contains("Hidden", violations[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>제작자 페이지 안내 기능이 막히지 않도록 허용한 프로젝트 페이지 링크는 통과시키는지 확인한다.</summary>
        [TestCase("https://github.com/owner/repo")]
        [TestCase("https://github.com/owner/repo/releases")]
        [TestCase("https://github.com/owner/repo/releases/tag/v1.0")]
        [TestCase("https://steamcommunity.com/sharedfiles/filedetails/?id=123")]
        public void LinkPolicy_AllowsProjectPages(string url)
        {
            Assert.IsTrue(ModLinkPolicy.TryGetSafeUrl(url, out _));
        }

        /// <summary>모드 링크로 실행 파일을 받게 하거나 계정 권한을 넘기게 하는 공격을 막는지, 비슷한 도메인·http도 거르는지 확인한다.</summary>
        [TestCase("https://github.com/owner/repo/releases/download/v1/setup.exe")]
        [TestCase("https://github.com/owner/repo/archive/main.zip")]
        [TestCase("https://github.com/owner/repo/raw/main/setup.exe")]
        [TestCase("https://github.com/login/device")]
        [TestCase("https://github.com/settings/tokens")]
        [TestCase("https://github.com/login/oauth/authorize?client_id=1")]
        [TestCase("https://github.com/owner/repo?tab=readme")]
        [TestCase("https://github.com/owner")]
        [TestCase("http://github.com/owner/repo")]
        [TestCase("https://github.com.evil.example/owner/repo")]
        [TestCase("https://steamcommunity.com/linkfilter/?url=https://evil.example")]
        public void LinkPolicy_RejectsOtherLinks(string url)
        {
            Assert.IsFalse(ModLinkPolicy.TryGetSafeUrl(url, out _));
        }

        /// <summary>PC마다 지문이 다르면 같은 모드를 다른 모드로 보므로, 버퍼 크기보다 큰 파일을 나눠 읽어도 이어 붙인 SHA-256과 같은지, 빈 이름은 건너뛰고 순서는 반영하는지 확인한다.</summary>
        [Test]
        public void ContentHash_MatchesConcatenatedSha256()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ModHashTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                byte[] largeFileBytes = Enumerable.Repeat((byte)7, 3 * 1024 * 1024 + 11).ToArray();
                byte[] smallFileBytes = { 1, 2, 3 };
                File.WriteAllBytes(Path.Combine(folder, "a"), largeFileBytes);
                File.WriteAllBytes(Path.Combine(folder, "b"), smallFileBytes);

                using SHA256 sha256 = SHA256.Create();
                byte[] digest = sha256.ComputeHash(largeFileBytes.Concat(smallFileBytes).ToArray());
                string expected = BitConverter.ToString(digest, 0, 8).Replace("-", string.Empty).ToLowerInvariant();

                Assert.AreEqual(expected, ModContentHash.Compute(folder, "a", string.Empty, "b"));
                Assert.AreNotEqual(expected, ModContentHash.Compute(folder, "b", "a"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
