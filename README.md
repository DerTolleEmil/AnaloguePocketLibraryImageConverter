# Convert images to an Analogue Pocket library compatible format
This tool can convert images to an Analog Pocket library compatible .bin format and vice-versa.

## Usage
Run the tool from any terminal and specify one or multiple input files / directories (or drag and drop them onto the binary). All images found will be converted to Analogue Pocket's .bin format and placed in a sub-direcory called "*converted*". All .bin files, if identified as Analogue Pocket library images, will be converted to regular .bmp Bitmap files.

## Options
``--no-rotate``
Images in the Pocket's .bin format are rotated by -90° degrees. The converter will automatically rotate the images, unless ``--no-rotate`` is specified.

``--output-dir=``
Specify the output directory. If not specified the converted images will be placed in a sub-directory "*converted*" inside the input directory.

# Warning
I don't own a Pocket and could not test this on real hardware. The images converted back and forth match the original file format before any conversion has been attempted, however, there are no sanity checks at all. I have no idea what the Pocket does when being fed a .bin file containing images far exceeding the resolutions found in [spiritualized1997's image set](https://www.reddit.com/r/AnaloguePocket/comments/wbduq6/analogue_pocket_library_image_set/), which is what I used for testing.

# .bin File format description
The format is extremely basic. All data is in [little-endian](https://en.wikipedia.org/wiki/Endianness).

The first four bytes are an identifier ``"API "`` (with a trailing space), presumably short for Analogue Pocket Image. The next 2 bytes are the image's width, then 2 more bytes for the image's height (after being rotated)*. The rest of the file contains the pixel data with 4 bytes per pixel (ARGB; or BGRA since the file is little-endian). The image itself is stored rotated by -90°.

*Note on image dimensions: I refer to the image dimensions after rotation as that makes more sense. For example, .bin images released for Gameboy games are 160x144 after rotation, which matches the Gameboy's display, although technically the image inside the .bin files is 144 pixels wide and 160 pixels high since it's rotated by 90°.