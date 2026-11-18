using System.Buffers.Binary;
using System.IO.Compression;
using SkiaSharp;

namespace RaceDay.Api.Services;

/// <summary>Checks the detected image format and fully decodes image pixels before upload.</summary>
public static class ImageContentValidator
{
    private const long MaximumPixelCount = 16_000_000;
    private const int MaximumImageDimension = 8192;

    public static bool TryValidate(
        string? fileName,
        string? declaredContentType,
        ReadOnlySpan<byte> content,
        out string extension,
        out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;
        if (content.IsEmpty) return false;

        try
        {
            using var data = SKData.CreateCopy(content.ToArray());
            using var codec = SKCodec.Create(data);
            if (codec is null || !TryGetFormat(codec.EncodedFormat, out var detectedExtension, out var detectedContentType))
            {
                return false;
            }

            var suppliedExtension = Path.GetExtension(fileName ?? string.Empty);
            var extensionMatches = string.Equals(suppliedExtension, detectedExtension, StringComparison.OrdinalIgnoreCase)
                || detectedExtension == ".jpg"
                    && string.Equals(suppliedExtension, ".jpeg", StringComparison.OrdinalIgnoreCase);
            if (!extensionMatches
                || !string.Equals(declaredContentType, detectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var imageInfo = codec.Info;
            if (imageInfo.Width <= 0 || imageInfo.Height <= 0
                || imageInfo.Width > MaximumImageDimension || imageInfo.Height > MaximumImageDimension
                || (long)imageInfo.Width * imageInfo.Height > MaximumPixelCount)
            {
                return false;
            }

            if (codec.EncodedFormat == SKEncodedImageFormat.Png
                && !HasValidPngPixelData(content, imageInfo.Width, imageInfo.Height))
            {
                return false;
            }

            if (codec.EncodedFormat == SKEncodedImageFormat.Webp && !HasValidWebpChunks(content))
            {
                return false;
            }

            using var bitmap = new SKBitmap(imageInfo);
            var decodeResult = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (decodeResult != SKCodecResult.Success)
            {
                return false;
            }

            extension = detectedExtension;
            contentType = detectedContentType;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryGetFormat(
        SKEncodedImageFormat format,
        out string extension,
        out string contentType)
    {
        (extension, contentType) = format switch
        {
            SKEncodedImageFormat.Jpeg => (".jpg", "image/jpeg"),
            SKEncodedImageFormat.Png => (".png", "image/png"),
            SKEncodedImageFormat.Webp => (".webp", "image/webp"),
            _ => (string.Empty, string.Empty)
        };
        return extension.Length > 0;
    }

    private static bool HasValidPngPixelData(ReadOnlySpan<byte> png, int width, int height)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 33 || !png[..8].SequenceEqual(signature)) return false;

        var bitDepth = png[24];
        var colorType = png[25];
        var interlace = png[28];
        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => 0
        };
        if (channels == 0 || interlace > 1) return false;

        using var compressed = new MemoryStream();
        var offset = 8;
        var hasHeader = false;
        while (offset <= png.Length - 12)
        {
            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset, 4));
            if (chunkLength > int.MaxValue) return false;
            var dataLength = (int)chunkLength;
            var chunkEnd = (long)offset + 12 + dataLength;
            if (chunkEnd > png.Length) return false;
            var type = png.Slice(offset + 4, 4);
            var data = png.Slice(offset + 8, dataLength);

            if (!hasHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) || dataLength != 13) return false;
                hasHeader = true;
            }

            if (type.SequenceEqual("IDAT"u8)) compressed.Write(data);
            offset = checked((int)chunkEnd);
            if (type.SequenceEqual("IEND"u8)) break;
        }

        if (!hasHeader || compressed.Length == 0) return false;
        var expectedOutputBytes = CalculatePngScanlineBytes(width, height, channels, bitDepth, interlace == 1);
        if (expectedOutputBytes <= 0 || expectedOutputBytes > 64L * 1024 * 1024) return false;

        try
        {
            compressed.Position = 0;
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += read;
                if (totalRead > expectedOutputBytes) return false;
            }

            return totalRead == expectedOutputBytes;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or OverflowException)
        {
            return false;
        }
    }

    private static long CalculatePngScanlineBytes(int width, int height, int channels, int bitDepth, bool interlaced)
    {
        long total = 0;
        ReadOnlySpan<int> startX = interlaced ? [0, 4, 0, 2, 0, 1, 0] : [0];
        ReadOnlySpan<int> startY = interlaced ? [0, 0, 4, 0, 2, 0, 1] : [0];
        ReadOnlySpan<int> stepX = interlaced ? [8, 8, 4, 4, 2, 2, 1] : [1];
        ReadOnlySpan<int> stepY = interlaced ? [8, 8, 8, 4, 4, 2, 2] : [1];

        for (var pass = 0; pass < startX.Length; pass++)
        {
            var passWidth = width <= startX[pass] ? 0 : (width - startX[pass] + stepX[pass] - 1) / stepX[pass];
            var passHeight = height <= startY[pass] ? 0 : (height - startY[pass] + stepY[pass] - 1) / stepY[pass];
            if (passWidth == 0 || passHeight == 0) continue;

            var rowBytes = ((long)passWidth * channels * bitDepth + 7) / 8;
            total = checked(total + passHeight * (rowBytes + 1));
        }

        return total;
    }

    private static bool HasValidWebpChunks(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("RIFF"u8)
            || !bytes.Slice(8, 4).SequenceEqual("WEBP"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) != bytes.Length - 8)
        {
            return false;
        }

        var offset = 12;
        var hasImageData = false;
        while (offset <= bytes.Length - 8)
        {
            var chunkType = bytes.Slice(offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var dataOffset = offset + 8;
            if (chunkSize > int.MaxValue || (long)dataOffset + chunkSize > bytes.Length) return false;
            var size = (int)chunkSize;
            var data = bytes.Slice(dataOffset, size);

            if (chunkType.SequenceEqual("VP8 "u8))
            {
                if (size < 10 || !data.Slice(3, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A })) return false;
                hasImageData = true;
            }
            else if (chunkType.SequenceEqual("VP8L"u8))
            {
                if (size < 5 || data[0] != 0x2F) return false;
                hasImageData = true;
            }
            else if (chunkType.SequenceEqual("VP8X"u8) && size != 10)
            {
                return false;
            }

            offset = checked(dataOffset + size + (size & 1));
        }

        return hasImageData && offset == bytes.Length;
    }
}
