using System.IO;

namespace RePKG.Core.Texture
{
    public interface ITexImageWriter
    {
        void WriteTo(BinaryWriter writer, Tex tex, TexImage mipmap);
    }
}