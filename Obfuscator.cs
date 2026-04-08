// IceObfuscator.cs
// NuGet deps: ZstdSharp.Port
// dotnet add package ZstdSharp.Port

using System;
using System.Text;
using ZstdSharp;

namespace IceObfuscator
{
    public static class Obfuscator
    {
        private const string BANNER = @"--[[
       d8888 888    d8P 
      d88888 888   d8P  
     d88P888 888  d8P   
    d88P 888 888d88K    
   d88P  888 8888888b   
  d88P   888 888  Y88b  
 d8888888888 888   Y88b 
d88P     888 888    Y88b
------------------------------
discord.gg/akadmin
]]--";

        /// <summary>
        /// Obfuscates Lua source using Zstd compression + ASCII85 encoding.
        /// Output is compatible with Roblox's Enum.CompressionAlgorithm.Zstd.
        /// </summary>
        public static ObfuscateResult Obfuscate(string luaSource)
        {
            if (string.IsNullOrEmpty(luaSource))
                throw new ArgumentException("Lua source cannot be empty.");

            byte[] sourceBytes = Encoding.UTF8.GetBytes(luaSource);

            // Zstd compress (level 3 matches default zstd-codec behaviour)
            byte[] compressed;
            using (var compressor = new Compressor(3))
                compressed = compressor.Wrap(sourceBytes).ToArray();

            // ASCII85 encode (little-endian word packing — matches the JS runtime decoder)
            string encoded = EncodeASCII85LE(compressed);
            string data    = "LPS/" + encoded;

            // Pick a Lua long-string delimiter that doesn't appear in the data
            string eq = PickLuaDelimiter(data);

            // Build the self-executing Lua runtime stub
            string runtime = BuildRuntime(data, eq, compressed.Length);

            return new ObfuscateResult
            {
                Output        = BANNER + "\n\n" + runtime,
                SourceBytes   = sourceBytes.Length,
                CompressedBytes = compressed.Length,
                EncodedLength = data.Length
            };
        }

        // ---------------------------------------------------------------
        // ASCII85 encoder — little-endian 32-bit words (matches JS impl)
        // ---------------------------------------------------------------
        private static string EncodeASCII85LE(byte[] bytes)
        {
            int padLen = ((bytes.Length + 3) / 4) * 4;
            byte[] padded = new byte[padLen];
            Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);

            var sb = new StringBuilder(padLen * 5 / 4 + 16);

            for (int i = 0; i < padded.Length; i += 4)
            {
                // little-endian: padded[i] is least-significant byte
                uint v = (uint)(
                    padded[i]
                    | (padded[i + 1] << 8)
                    | (padded[i + 2] << 16)
                    | ((uint)padded[i + 3] << 24));

                if (v == 0)
                {
                    sb.Append('z');
                }
                else
                {
                    Span<char> chunk = stackalloc char[5];
                    uint n = v;
                    for (int j = 4; j >= 0; j--)
                    {
                        chunk[j] = (char)((n % 85) + 33);
                        n /= 85;
                    }
                    sb.Append(chunk);
                }
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // Find the shortest Lua long-string level that doesn't collide
        // ---------------------------------------------------------------
        private static string PickLuaDelimiter(string data)
        {
            int n = 0;
            while (data.Contains("]" + new string('=', n) + "]"))
                n++;
            return new string('=', n);
        }

        // ---------------------------------------------------------------
        // Build the Lua runtime stub (mirrors the JS obfuscate() function)
        // ---------------------------------------------------------------
        private static string BuildRuntime(string data, string eq, int compressedLen)
        {
            return
                $"return(function(a,b,c,d,e,f,g,h,i,j,k,l,m,n)" +
                $"local function o(p)" +
                $"local j=e(p,\"[^!-uz]\",j)" +
                $"j=e(j,\"z\",\"!!!!!\")local c=i({{}},{{__index=function(i,p)" +
                $"local c,q,r,s,t=c(p,1,5)" +
                $"local c=(t-33)+(s-33)*85+(r-33)*7225+(q-33)*614125+(c-33)*52200625 " +
                $"local q=c%256;c=g(c/256)" +
                $"local r=c%256;c=g(c/256)" +
                $"local s=c%256;c=g(c/256)" +
                $"local c=c%256 local c=f(q,r,s,c)i[p]=c return c end}})" +
                $"local c=e(j,\".....\",c)return d(c,1,b)end " +
                $"local a=l(o(d(a,5)))local a=n:DecompressBuffer(a,h)" +
                $"local a=m(a)return k(a)end)" +
                $"([{eq}[{data}]{eq}]," +
                $"{compressedLen}," +
                $"string.byte,string.sub,string.gsub,string.char," +
                $"math.floor,Enum.CompressionAlgorithm.Zstd,setmetatable,\"\"," +
                $"(loadstring or load),buffer.fromstring,buffer.tostring," +
                $"game:GetService(\"EncodingService\"))(...)" ;
        }
    }

    public class ObfuscateResult
    {
        public string Output          { get; set; } = "";
        public int    SourceBytes     { get; set; }
        public int    CompressedBytes { get; set; }
        public int    EncodedLength   { get; set; }
        public double Ratio           => SourceBytes > 0 ? (double)SourceBytes / CompressedBytes : 0;
    }
}

// ---------------------------------------------------------------
// Optional: ASP.NET minimal-API endpoint
// ---------------------------------------------------------------
// In Program.cs:
//
//   app.MapPost("/obfuscate", (ObfuscateRequest req) =>
//   {
//       try
//       {
//           var result = Obfuscator.Obfuscate(req.Source);
//           return Results.Ok(new
//           {
//               output          = result.Output,
//               sourceBytes     = result.SourceBytes,
//               compressedBytes = result.CompressedBytes,
//               encodedLength   = result.EncodedLength,
//               ratio           = Math.Round(result.Ratio, 2)
//           });
//       }
//       catch (Exception ex)
//       {
//           return Results.BadRequest(new { error = ex.Message });
//       }
//   });
//
//   record ObfuscateRequest(string Source);

