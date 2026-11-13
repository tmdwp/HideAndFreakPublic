using System.IO;

namespace GuardVsMannequin.Mods
{
    /// <summary>모드 하나의 상태.</summary>
    public enum ModStatus : byte
    {
        /// <summary>켜져 있고 쓸 수 있다.</summary>
        Active,

        /// <summary>플레이어가 껐다.</summary>
        Disabled,

        /// <summary>오류로 쓸 수 없는 상태.</summary>
        Error
    }

    /// <summary>모드 화면과 모드 일치 확인이 쓰는 설치된 모드 하나.</summary>
    public sealed class ModInfo
    {
        /// <summary>정보 파일을 읽지 못한 모드도 목록에 오류로 보여 주려고 null을 허용한다.</summary>
        public ModManifest Manifest { get; }

        public string FolderPath { get; }

        public ModStatus Status { get; private set; }

        /// <summary>모드 화면에서 무엇이 잘못됐는지 보여 주려고 남긴다.</summary>
        public string Error { get; private set; }

        /// <summary>정보 파일을 읽지 못해도 구분할 수 있도록 폴더 이름으로 대신한다.</summary>
        public string Id => Manifest?.id ?? Path.GetFileName(FolderPath);

        /// <summary>이름이 비어 있어도 목록이 빈칸이 되지 않도록 ID로 대신한다.</summary>
        public string DisplayName => string.IsNullOrEmpty(Manifest?.name) ? Id : Manifest.name;

        /// <summary>검사 전에는 쓸 수 있는 것으로 시작하고, 검사에서 문제가 나오면 오류로 바꾼다.</summary>
        /// <param name="manifest">읽지 못했으면 null.</param>
        public ModInfo(ModManifest manifest, string folderPath)
        {
            Manifest = manifest;
            FolderPath = folderPath;
            Status = ModStatus.Active;
        }

        /// <summary>
        /// 오류가 난 모드를 쓰지 않으면서도 목록에서는 이유를 보여 주도록 상태와 내용을 함께 남긴다.
        /// </summary>
        public void MarkError(string error)
        {
            Status = ModStatus.Error;
            Error = error;
        }

        /// <summary>
        /// 오류가 있는 모드를 껐다가 다시 켜면 문제를 모른 채 쓰게 되므로 오류 상태는 덮어쓰지 않는다.
        /// </summary>
        public void MarkDisabled()
        {
            if (Status != ModStatus.Error)
                Status = ModStatus.Disabled;
        }

        /// <summary>
        /// 정보 파일에 적힌 이름은 비어 있을 수 있어서, 비면 경로를 만들지 않고 null을 준다.
        /// </summary>
        public string GetFilePath(string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? null : Path.Combine(FolderPath, fileName);
        }
    }
}
