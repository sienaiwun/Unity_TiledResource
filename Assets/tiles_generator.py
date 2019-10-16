from PIL import Image
import os
import math
import shutil

from image_generator import texture_size
tiles_dir_name = "StreamingAssets"
cache_dir_name_prefix = "cache"
table_Size = 32
padding_size = 0
dimension_size = texture_size

class PixelData:
    def __init__(self, size, path, name):
        self._component_count = 3
        self._component_size = 1 # in char
        self._pixel_size = self._component_count * self._component_size #in char
        self._size = size
        self._path = path
        self._filename = name
        file_size = self._size * self._pixel_size
        for row in xrange(self._size):
            data_file_name = self.get_file_path(row)
            if not os.path.exists(data_file_name):
                open(data_file_name, "w+")
            with open(data_file_name,"r+") as f:
                old_size = os.path.getsize(data_file_name)
                if old_size != file_size:
                    self.__set_file_size(f, size)

    def __set_file_size(self, f, size):
        f.seek(size - 1)
        f.write(b"\0")

    def __clamp(self, value,  min_value, max_value):
        return max(min(value, max_value), min_value)

    def __block_copy(self, src, src_offset, dest, dest_offset, length):
        for i in xrange(length):
            if dest_offset + i < len(dest) and src_offset + i < len(src):
                dest[dest_offset + i] = src[src_offset + i]

    def get_file_path(self, row):
        file_path = self._path + os.path.sep + "{0}_{1}".format(self._filename, row)
        return file_path

    def set_pixels(self, x, y, block_width, block_height, colors):
        for row in xrange(block_height):
            data_file_name = self.get_file_path(row + y)
            if not os.path.exists(data_file_name):
                open(data_file_name, "w+")
            with open(data_file_name,"r+") as f:
                f.seek(x*self._pixel_size)
                begin = row * block_width * self._pixel_size
                end = begin + block_width * self._pixel_size
                write_byte = colors[begin:end]
                byte_array = bytearray(write_byte)
                f.write(byte_array)

    def shuffle_height(self, h, blockheight, mip):
        mode = blockheight /int(pow(2,mip))
        index = h/mode
        offset = h%mode
        chunk_num = blockheight/mode
        chunk_height = blockheight/chunk_num
        shuffleh = (chunk_num -1 - index)*chunk_height + offset
        return shuffleh

    def get_pixels(self, x, y, block_width, block_height,mip):
        pixels = [b'\x00']* block_width * block_height*self._pixel_size
        for h in xrange(block_height):
            row = self.__clamp(y + h, 0, self._size - 1)
            read_data_file = self.get_file_path(row)
            with open(read_data_file, "rb") as f:
                h = self.shuffle_height(h,block_height,mip)
                self.__get_pixels(x,block_width, f, pixels, h*block_width*self._pixel_size)
        return pixels

    def __get_pixels(self, x, block_width, f_reader, pixels, pixeloffset):
        begin = self.__clamp(x,0, self._size -1)
        length = min(block_width + x-begin, self._size-begin)
        f_reader.seek(begin*self._pixel_size)
        buf_length = length * self._pixel_size
        buf = f_reader.read(buf_length)
        self.__block_copy(buf,0,pixels,pixeloffset+(begin-x)*self._pixel_size, buf_length)
        if x < 0:
            for i in xrange(begin-x):
                self.__block_copy(buf,0,pixels,pixeloffset + i*self._pixel_size,self._pixel_size)
        if length < block_width:
            for i in xrange(block_width- length):
                self.__block_copy(buf, buf_length - self._pixel_size, pixels, pixeloffset + (length+i)*self._pixel_size, self._pixel_size)



def get_pixel_data(mip_level):
    cur_location = os.getcwd()
    cache_dir_name = cur_location + os.path.sep + cache_dir_name_prefix
    if not os.path.isdir(cache_dir_name):
        os.mkdir(cache_dir_name)
    size = table_Size * dimension_size >> mip_level
    data_name = "mip_{0}".format(mip_level)
    pixel_data = PixelData(size,cache_dir_name, data_name)
    return pixel_data

def data_from_image(img):
    img_width, img_height = img.size
    pixels = list(img.getdata())
    pixels = [pixels[i * img_width:(i + 1) * img_width] for i in xrange(img_height)]
    import itertools
    pixels_data = list(itertools.chain.from_iterable(pixels))
    pixels_data = list(itertools.chain(*pixels_data))
    return pixels_data

def generate_mip0():
    from image_generator import texture_size, image_dir_name, image_name_prefix
    cur_location = os.getcwd()
    pixel_data = get_pixel_data(0)
    for row in xrange(table_Size):
        for col in xrange(table_Size):
            number_str = '{:03d}'.format(row * table_Size + col)
            input_file_string = cur_location + os.path.sep + image_dir_name + os.path.sep + image_name_prefix + number_str + '.png'
            if not os.path.exists(input_file_string):
                continue
            img = Image.open(input_file_string)
            pixels_data = data_from_image(img)
            img_width, img_height = img.size
            pixel_data.set_pixels(col*texture_size, row*texture_size,img_width, img_height, pixels_data)
    print("Generate Mip0Cache Done.")

def generate_mip(mip):
    input_data = get_pixel_data(mip-1)
    output_data = get_pixel_data(mip)
    patch_size = dimension_size
    double_patch_size = patch_size *2
    patch_count = output_data._size / patch_size
    for row in xrange(patch_count):
        for col in xrange(patch_count):
            input_pixel = input_data.get_pixels(col*double_patch_size,row*double_patch_size,double_patch_size,double_patch_size,0)
            input_img = Image.frombytes("RGB",(double_patch_size,double_patch_size),''.join(input_pixel))
            input_img.thumbnail((patch_size,patch_size))
            #input_img.show()
            output_img_data = data_from_image(input_img)
            output_data.set_pixels(col*patch_size,row*patch_size,patch_size,patch_size,output_img_data)

def output_data(mip):
    print ("output mip"+str(mip))
    img_data = get_pixel_data(mip)
    size_with_padding = dimension_size + padding_size*2
    page_count = img_data._size/ dimension_size
    cur_location = os.getcwd()
    dir_name = cur_location + os.path.sep + tiles_dir_name
    for row in xrange(page_count):
        for col in xrange(page_count):
            pixel_data = img_data.get_pixels(
                col * dimension_size - padding_size,
                row * dimension_size - padding_size,
                size_with_padding,
                size_with_padding,
                mip
            )
            output_img = Image.frombytes(mode = 'RGB',size = (size_with_padding,size_with_padding), data = ''.join(pixel_data))
            img_file_name = "Tiles_MIP{2}_Y{1}_X{0}.png".format( col, row, mip)
            output_img.save(dir_name + os.path.sep + img_file_name)


def tiles():
    cur_location = os.getcwd()
    tile_dir_name = cur_location + os.path.sep + tiles_dir_name
    if os.path.isdir(tile_dir_name):
        shutil.rmtree(tile_dir_name)
    cache_dir_name = cur_location + os.path.sep + cache_dir_name_prefix
    if os.path.isdir(cache_dir_name):
        shutil.rmtree(cache_dir_name)
    os.mkdir(tile_dir_name)
    maxLevel = int(math.log(table_Size, 2)) + 1
    for mip in xrange(maxLevel):
        if mip == 0:
            generate_mip0()
        else:
            generate_mip(mip)
        output_data(mip)

if __name__ == "__main__":
    '''img = Image.open("output/black.png")
    size = img.size
    raw = img.tobytes()
    #b = bytes(raw, 'utf-8')


    #st = str(b)
    pixel_data = b'\xff' * 12
    list1 = list(pixel_data)
    list1[0] = b'\x00'
    #list1[1] = b'\x00'
    #list1[2] = b'\x00'
    list1[3] = b'\x00'
    list1[4] = b'\x00'
    list1[5] = b'\x00'
    st = ''.join(list1)
    st2 = str(list1)
    import io
    output_img = Image.frombytes('RGB', size=(2, 2), data=str(list1))
    #output_img = Image.open(io.BytesIO(pixel_data))
    output_img.show()
    '''
    tiles()