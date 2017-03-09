﻿using System;
using System.IO;
using System.Text;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Converters;

namespace SharpCompress.Writers.Zip
{
    internal class ZipCentralDirectoryEntry
    {
        internal string FileName { get; set; }
        internal DateTime? ModificationTime { get; set; }
        internal string Comment { get; set; }
        internal uint Crc { get; set; }
        internal ulong HeaderOffset { get; set; }
        internal ulong Compressed { get; set; }
        internal ulong Decompressed { get; set; }
        internal ushort Zip64HeaderOffset { get; set; }

        internal uint Write(Stream outputStream, ZipCompressionMethod compression)
        {
            byte[] encodedFilename = Encoding.UTF8.GetBytes(FileName);
            byte[] encodedComment = Encoding.UTF8.GetBytes(Comment);

            var zip64 = Compressed >= uint.MaxValue || Decompressed >= uint.MaxValue || HeaderOffset >= uint.MaxValue || Zip64HeaderOffset != 0;

            var compressedvalue = zip64 ? uint.MaxValue : (uint)Compressed;
            var decompressedvalue = zip64 ? uint.MaxValue : (uint)Decompressed;
            var headeroffsetvalue = zip64 ? uint.MaxValue : (uint)HeaderOffset;
            var extralength = zip64 ? (2 + 2 + 8 + 8 + 8 + 4) : 0;
            var version = (byte)(zip64 ? 45 : 10);

            HeaderFlags flags = HeaderFlags.UTF8;
            if (!outputStream.CanSeek)
            {
                // Cannot use data descriptors with zip64:
                // https://blogs.oracle.com/xuemingshen/entry/is_zipinput_outputstream_handling_of
                if (!zip64)
                    flags |= HeaderFlags.UsePostDataDescriptor;
                
                if (compression == ZipCompressionMethod.LZMA)
                {
                    flags |= HeaderFlags.Bit1; // eos marker
                }
            }

            //constant sig, then version made by, compabitility, then version to extract
            outputStream.Write(new byte[] { 80, 75, 1, 2, 0x14, 0, version, 0 }, 0, 8);

            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)flags), 0, 2);
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)compression), 0, 2); // zipping method
            outputStream.Write(DataConverter.LittleEndian.GetBytes(ModificationTime.DateTimeToDosTime()), 0, 4);

            // zipping date and time
            outputStream.Write(DataConverter.LittleEndian.GetBytes(Crc), 0, 4); // file CRC
            outputStream.Write(DataConverter.LittleEndian.GetBytes(compressedvalue), 0, 4); // compressed file size
            outputStream.Write(DataConverter.LittleEndian.GetBytes(decompressedvalue), 0, 4); // uncompressed file size
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)encodedFilename.Length), 0, 2); // Filename in zip
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)extralength), 0, 2); // extra length
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)encodedComment.Length), 0, 2);

            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0), 0, 2); // disk=0
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0), 0, 2); // file type: binary
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0), 0, 2); // Internal file attributes
            outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0x8100), 0, 2);

            // External file attributes (normal/readable)
            outputStream.Write(DataConverter.LittleEndian.GetBytes(headeroffsetvalue), 0, 4); // Offset of header

            outputStream.Write(encodedFilename, 0, encodedFilename.Length);
            if (zip64)
            {
                outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0x0001), 0, 2);
                outputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)(extralength - 4)), 0, 2);

                outputStream.Write(DataConverter.LittleEndian.GetBytes(Decompressed), 0, 8);
                outputStream.Write(DataConverter.LittleEndian.GetBytes(Compressed), 0, 8);
                outputStream.Write(DataConverter.LittleEndian.GetBytes(HeaderOffset), 0, 8);
                outputStream.Write(DataConverter.LittleEndian.GetBytes(0), 0, 4); // VolumeNumber = 0
            }

            outputStream.Write(encodedComment, 0, encodedComment.Length);

            return (uint)(8 + 2 + 2 + 4 + 4 + 4 + 4 + 2 + 2 + 2
                                    + 2 + 2 + 2 + 2 + 4 + encodedFilename.Length + extralength + encodedComment.Length);
        }
    }
}