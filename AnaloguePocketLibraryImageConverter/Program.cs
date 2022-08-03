using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace AnalogueLibImage
{
    internal class Program
    {
        private static readonly CancellationTokenSource _cts = new();
        static void ExitWithHelp()
        {
            Console.WriteLine($"No (valid) input files / directories specified.\r\n\r\nOptions:\r\n\t--no-rotate\t\tDo not rotate images.\r\n\t--output-dir\t\tOutput directory (use --output-dir=\"path{Path.DirectorySeparatorChar}to{Path.DirectorySeparatorChar}output\"). Will be created if it does not exist. Defaults to \"converted\" in the input directory.");
            Environment.Exit(1);
        }
        static async Task Main(string[] args)
        {
            // set up console so we can process ctrl+c ourselves
            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
            {
                _cts.Cancel();
                e.Cancel = true;
            };

            // vars
            bool doNotRotate = false;
            var ct = _cts.Token;
            List<string> files = new();
            string? outputPath = null;
            byte[] pocketImageHeader = new byte[4] { 0x20, 0x49, 0x50, 0x41 };

            // no arguments
            if (args.Length == 0) ExitWithHelp();
            
            // parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--no-rotate")
                {
                    doNotRotate = true;
                }
                else if (args[i].StartsWith("--output-dir="))
                {
                    outputPath = args[i].Split('=')[1];
                }
                else if (Directory.Exists(args[i]))
                {
                    files.AddRange(Directory.GetFiles(args[i]));
                }
                else if (File.Exists(args[i]))
                {
                    files.Add(args[i]);
                }
            }
            // no files found after parsing arguments
            if (files.Count == 0) ExitWithHelp();


            // check specified output path
            if (outputPath is not null && !Directory.Exists(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot create the specified output directory \"{outputPath}\".\r\n{ex.Message}");
                    Environment.Exit(2);
                }
            }


            var options = new ParallelOptions() { CancellationToken = _cts.Token };
            try
            {
                // process all files in parallel
                await Parallel.ForEachAsync(files, options, async (file, token) =>
                {
                    try
                    {
                        using var inputStream = File.OpenRead(file);
                        var buffer = new byte[4];
                        await inputStream.ReadAsync(buffer.AsMemory(), token);
                        if (buffer.SequenceEqual(pocketImageHeader))
                        {
                            // pocket library file
                            Console.WriteLine($"Converting {file}");
                            await inputStream.ReadAsync(buffer.AsMemory(), token);
                            // convert from little-endian
							int width = buffer[1] << 8 | buffer[0];
                            int height = buffer[3] << 8 | buffer[2];
							// read pixel data
                            var contentBuffer = new byte[width * height * 4];
                            await inputStream.ReadAsync(contentBuffer.AsMemory(), token);
                            
							// swap height and width, the raw image is rotated
                            using var image = Image.LoadPixelData<Bgra32>(contentBuffer, height, width);
                            if (!doNotRotate) image.Mutate(x => x.Rotate(90));

                            if (!TryCreateOutputDir(outputPath, file, out string outDir)) throw new Exception($"Cannot create directory {outDir}.");
                            await image.SaveAsBmpAsync(Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".bmp"), token);
                            
                        }
                        else
                        {
                            // image, hopefully
                            if (await Image.IdentifyAsync(file, token) is null) return;
                            Console.WriteLine($"Converting {file}");
                            
                            // load image and convert to BGRA32 pixel format used by Pocket files
                            using var image = await Image.LoadAsync<Bgra32>(file, token);
                            if (!TryCreateOutputDir(outputPath, file, out string outDir)) throw new Exception($"Cannot create directory {outDir}.");
                            using var outputStream = File.OpenWrite(Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".bin"));
                            await outputStream.WriteAsync(pocketImageHeader, token);
                            // get dimensions as bytes
                            var heightRaw = BitConverter.GetBytes((Int16)image.Height);
                            var widthRaw = BitConverter.GetBytes((Int16)image.Width);
                            
                            // convert to little endian if host machine is big endian
                            if (!BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(widthRaw);
                                Array.Reverse(heightRaw);
                            }

                            // write image dimensions; swap width and height depending on rotation
                            if (doNotRotate)
                            {
                                await outputStream.WriteAsync(heightRaw, token);
                                await outputStream.WriteAsync(widthRaw, token);
                            }
                            else
                            {
                                image.Mutate(x => x.Rotate(-90));
                                await outputStream.WriteAsync(widthRaw, token);
                                await outputStream.WriteAsync(heightRaw, token);
                            }
                            // get and write pixel data
                            byte[] pixelData = new byte[image.Width * image.Height * 4];
                            image.CopyPixelDataTo(pixelData);
                            await outputStream.WriteAsync(pixelData, token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error converting \"{file}\".\r\n{ex.Message}.");
                    }
                });
            }
            catch (TaskCanceledException) { }

        }

		static bool TryCreateOutputDir(string? outputPath, string fileName, out string path)
		{
			path = outputPath ?? Path.Combine(Path.GetDirectoryName(fileName)!, "converted");
			try
			{
				Directory.CreateDirectory(path);
                return true;
			} catch {
                return false;
			}
		}

    }
}