#region LICENSE

// Copyright 2014 - 2014 InibinSharp
// Inibin.cs is part of InibinSharp.
// InibinSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// InibinSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with InibinSharp. If not, see <http://www.gnu.org/licenses/>.

#endregion

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using InibinSharp.RAF;

#endregion

namespace InibinSharp
{
    /// <summary>
    ///     Port of
    ///     https://github.com/Elyotna/IntWars/blob/d8d4a6d369f294a227f78fe096119444cc315cfb/dep/include/raf/Inibin.h
    /// </summary>
    public class Inibin : IDisposable
    {
        private readonly int _stringOffset;
        private readonly BinaryReader _reader;
        public readonly Dictionary<UInt32, Object> Values = new Dictionary<uint, Object>();

        public Inibin(byte[] data)
            : this(new MemoryStream(data))
        {
        }

        public Inibin(string filePath)
            : this(File.ReadAllBytes(filePath))
        {
        }

        public Inibin(RAFFileListEntry file)
            : this(file.GetContent())
        {
        }

        public Inibin(Stream stream)
        {
            _reader = new BinaryReader(stream);

            var size = (int) _reader.BaseStream.Length;
            var version = ReadValue<byte>();
            var oldLength = ReadValue<UInt16>();
            var bitmask = ReadValue<UInt16>();
            _stringOffset = size - oldLength;

            Debug.WriteLine("Version:" + version);
            Debug.WriteLine("Length:" + size);
            Debug.WriteLine("OldLength:" + oldLength);
            Debug.WriteLine("StringOffset:" + _stringOffset);
            Debug.WriteLine("Bitmask:" + bitmask);
            Debug.WriteLine("");

            if (version != 2)
            {
                throw new InvalidDataException("Wrong Ininbin version: " + version);
            }

            if ((bitmask & 0x0001) != 0)
            {
                ParseValues<UInt32>();
            }

            if ((bitmask & 0x0002) != 0)
            {
                ParseValues<float>();
            }

            if ((bitmask & 0x0004) != 0)
            {
                ParseValues<byte>(true);
            }

            if ((bitmask & 0x0008) != 0)
            {
                ParseValues<UInt16>();
            }

            if ((bitmask & 0x0010) != 0)
            {
                ParseValues<byte>();
            }

            if ((bitmask & 0x0020) != 0)
            {
                ParseValues<bool>();
            }

            if ((bitmask & 0x0040) != 0)
            {
                SkipValues(4 + 3);
            }

            if ((bitmask & 0x0080) != 0)
            {
                SkipValues(4 + 12);
            }

            if ((bitmask & 0x0100) != 0)
            {
                ParseValues<UInt16>();
            }

            if ((bitmask & 0x0200) != 0)
            {
                SkipValues(4 + 8);
            }

            if ((bitmask & 0x0400) != 0)
            {
                ParseValues<UInt32>();
            }

            if ((bitmask & 0x0800) != 0)
            {
                SkipValues(4 + 16);
            }

            if ((bitmask & 0x1000) != 0)
            {
                ParseValues<string>();
            }
        }

        private void AddValue<T>(UInt32 key, T value)
        {
            Values.Add(key, value);
            Debug.WriteLine("{0} [{1}] = {2}", typeof (T).Name, key, value);
        }

        private void SkipValues(int size)
        {
            var start = _reader.BaseStream.Position;
            var keys = ReadSegmentKeys();
            _reader.BaseStream.Position += keys.Length*size;
            Debug.WriteLine("{0} properties skip from {1} to {2}", size, start, _reader.BaseStream.Position);
        }

        private void ParseValues<T>(bool isBase10 = false)
        {
            Debug.WriteLine("{0} properties start position {1}", typeof (T).Name, _reader.BaseStream.Position);
            var keys = ReadSegmentKeys();

            if (typeof (T) == typeof (bool))
            {
                var index = 0;
                for (var i = 0; i < 1 + ((keys.Length - 1)/8); ++i)
                {
                    int bits = ReadValue<byte>();
                    for (var b = 0; b < 8; ++b)
                    {
                        var key = keys[index];
                        var val = (0x1 & bits) != 0;
                        AddValue(key, val);
                        bits = bits >> 1;
                        if (++index == keys.Length)
                        {
                            break;
                        }
                    }
                }
            }
            else if (typeof (T) == typeof (string))
            {
                foreach (var key in keys)
                {
                    var offset = ReadValue<UInt16>();
                    AddValue(key, ReadValue<string>(_stringOffset + offset));
                }
            }
            else
            {
                foreach (var key in keys)
                {
                    if (isBase10)
                    {
                        AddValue(key, ((byte) (object) ReadValue<T>())*0.1f);
                    }
                    else
                    {
                        AddValue(key, ReadValue<T>());
                    }
                }
            }

            Debug.WriteLine("");
        }

        private T ReadValue<T>(int offset = 0)
        {
            try
            {
                if (typeof (T) == typeof (byte))
                {
                    return (T) (object) _reader.ReadByte();
                }
                if (typeof (T) == typeof (UInt16))
                {
                    return (T) (object) _reader.ReadUInt16();
                }
                if (typeof (T) == typeof (UInt32))
                {
                    return (T) (object) _reader.ReadUInt32();
                }
                if (typeof (T) == typeof (float))
                {
                    return (T) (object) _reader.ReadSingle();
                }
                if (typeof (T) == typeof (string))
                {
                    int c;
                    var sb = new StringBuilder();
                    var oldPos = _reader.BaseStream.Position;
                    _reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    while ((c = _reader.ReadByte()) > 0)
                    {
                        sb.Append((char) c);
                    }
                    _reader.BaseStream.Seek(oldPos, SeekOrigin.Begin);

                    return (T) (object) sb.ToString();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return default(T);
        }

        private UInt32[] ReadSegmentKeys()
        {
            var result = new UInt32[ReadValue<UInt16>()];

            for (var i = 0; i < result.Length; ++i)
            {
                result[i] = ReadValue<UInt32>();
            }

            return result;
        }

        public T GetValue<T>(string section, string name)
        {
            try
            {
                if (!KeyExists(section, name))
                {
                    return default(T);
                }

                var key = GetKeyHash(section, name);

                if (typeof (T) == typeof (byte))
                {
                    return (T) (object) byte.Parse(Values[key].ToString());
                }
                if (typeof (T) == typeof (UInt16))
                {
                    return (T) (object) UInt16.Parse(Values[key].ToString());
                }
                if (typeof (T) == typeof (UInt32))
                {
                    return (T) (object) UInt32.Parse(Values[key].ToString());
                }
                if (typeof (T) == typeof (float))
                {
                    return (T) (object) float.Parse(Values[key].ToString());
                }
                if (typeof (T) == typeof (string))
                {
                    return (T) (object) Values[key].ToString();
                }
            }
            catch (Exception e)
            {
                return default(T);
            }

            return default(T);
        }

        public bool KeyExists(string section, string name)
        {
            return Values.ContainsKey(GetKeyHash(section, name));
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }
        }

        public static UInt32 GetKeyHash(string section, string name)
        {
            UInt32 hash = 0;

            foreach (var c in section.ToLower())
            {
                hash = c + 65599*hash;
            }

            hash = (65599*hash + 42);

            foreach (var c in name.ToLower())
            {
                hash = c + 65599*hash;
            }

            return hash;
        }
    }
}