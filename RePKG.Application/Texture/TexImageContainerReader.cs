using System;
using System.IO;
using RePKG.Application.Exceptions;
using RePKG.Core.Texture;

namespace RePKG.Application.Texture
{
    public class TexImageContainerReader : ITexImageContainerReader
    {
        private readonly ITexImageReader _texImageReader;

        public TexImageContainerReader(ITexImageReader texImageReader)
        {
            _texImageReader = texImageReader;
        }

        public TexImageContainer ReadFrom(BinaryReader reader, TexFormat texFormat)
        {
            var container = new TexImageContainer
            {
                Magic = reader.ReadNString(maxLength: 16)
            };

            var imageCount = reader.ReadInt32();

            if (imageCount > Constants.MaximumImageCount)
                throw new UnsafeTexException(
                    $"Image count exceeds limit: {imageCount}/{Constants.MaximumImageCount}");
 
            switch (container.Magic)
            {
                case "TEXB0001":
                case "TEXB0002":
                    break;

                case "TEXB0003":
                    container.ImageFormat = (FreeImageFormat) reader.ReadInt32();
                    break;

                default:
                    throw new UnknownTexImageContainerMagicException(container.Magic);
            }

            container.ImageContainerVersion = (TexImageContainerVersion) Convert.ToInt32(container.Magic.Substring(4));
            container.ImageFormat.AssertValid();
            
            for (var i = 0; i < imageCount; i++)
            {
               container.Images.Add(_texImageReader.ReadFrom(reader, container, texFormat));
            }

            return container;
        }
    }
}