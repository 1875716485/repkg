using System.IO;
using RePKG.Application.Exceptions;
using RePKG.Core.Texture;

namespace RePKG.Application.Texture
{
    public class TexFrameInfoContainerReader : ITexFrameInfoContainerReader
    {
        public TexFrameInfoContainer ReadFrom(BinaryReader reader)
        {
            var container = new TexFrameInfoContainer
            {
                Magic = reader.ReadNString(maxLength: 16),
                FrameCount = reader.ReadInt32()
            };

            container.Frames = new TexFrameInfo[container.FrameCount];

            switch (container.Magic)
            {
                case "TEXS0002":
                    ReadV2(container, reader);
                    break;

                case "TEXS0003":
                    ReadV3(container, reader);
                    break;

                default:
                    throw new UnknownTexFrameInfoContainerMagicException(container.Magic);
            }

            return container;
        }

        private static void ReadV2(TexFrameInfoContainer container, BinaryReader reader)
        {
            ReadFrames(container, reader);
        }

        private static void ReadV3(TexFrameInfoContainer container, BinaryReader reader)
        {
            container.Unk0 = reader.ReadInt32();
            container.Unk1 = reader.ReadInt32();

            ReadFrames(container, reader);
        }

        private static void ReadFrames(TexFrameInfoContainer container, BinaryReader reader)
        {
            for (var i = 0; i < container.FrameCount; i++)
            {
                container.Frames[i] = new TexFrameInfo
                {
                    ImageId = reader.ReadInt32(),
                    Frametime = reader.ReadSingle(),
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Width = reader.ReadSingle(),
                    Unk0 = reader.ReadSingle(),
                    Unk1 = reader.ReadSingle(),
                    Height = reader.ReadSingle(),
                };
            }
        }
    }
}