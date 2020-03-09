from PIL import Image
from PIL import ImageDraw
from PIL import ImageFont
import os
image_dir_name = "images"
texture_scale = 1
image_name_prefix = "image_"


def to_scale_size(input):
    return (int)(input*texture_scale)

texture_size = to_scale_size(512)


def image_char(char, image_size, font_size, outline_lenght, number_str):
        img = Image.new("RGB", (image_size, image_size), (0,0,0))
        draw = ImageDraw.Draw(img)
        draw.rectangle([(outline_lenght, outline_lenght), (image_size-outline_lenght, image_size-outline_lenght)], fill =(255,255,255) )
        font_path = "C:\Windows\Fonts\Arial.ttf"
        font = ImageFont.truetype(font_path, font_size)
        draw.text((to_scale_size(5), to_scale_size(135)), char, (0,0,0),font=font)
        save_location = os.getcwd()
        dir_name = save_location + os.path.sep + image_dir_name
        if not os.path.isdir(dir_name):
                os.mkdir(dir_name)
        img.save(dir_name + os.path.sep + number_str + '.png')


if __name__ == "__main__":
        for i in xrange(1024):
            number_str = '{:3d}'.format(i)
            file_str = '{:03d}'.format(i)
            image_char(number_str, image_size = texture_size, font_size = to_scale_size(300), outline_lenght =0, number_str = image_name_prefix+file_str)

