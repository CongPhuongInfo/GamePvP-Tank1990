"""
Tu sinh sprite pixel-art 16x16 cho Tank1990Online (thiet ke goc, khong sao chep
asset Battle City/Namco). Chay 1 lan de tao file PNG trong thu muc sprites/.
"""
from PIL import Image, ImageDraw

S = 16  # kich thuoc canvas sprite (pixel)


def new_canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def make_tank(body, dark, track, turret, barrel, highlight):
    """Ve tank huong len tren (Up). Cac huong khac se duoc xoay luc runtime."""
    img = new_canvas()
    d = ImageDraw.Draw(img)

    # Xich xe 2 ben (3px rong), co van xich ngang
    d.rectangle([0, 2, 2, 15], fill=track)
    d.rectangle([13, 2, 15, 15], fill=track)
    for y in range(3, 16, 2):
        d.line([(0, y), (2, y)], fill=highlight)
        d.line([(13, y), (15, y)], fill=highlight)

    # Than xe
    d.rectangle([3, 4, 12, 15], fill=body)
    d.rectangle([3, 4, 12, 4], fill=dark)      # vien tren
    d.rectangle([3, 15, 12, 15], fill=dark)    # vien duoi
    d.rectangle([3, 4, 3, 15], fill=dark)      # vien trai
    d.rectangle([12, 4, 12, 15], fill=dark)    # vien phai

    # Thap phao (hinh vuong giua than)
    d.rectangle([5, 6, 10, 11], fill=turret)
    d.rectangle([6, 7, 9, 10], fill=highlight)

    # Nong sung huong len tren
    d.rectangle([7, 0, 8, 6], fill=barrel)

    return img


def make_brick():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    base = (156, 92, 44, 255)
    mortar = (90, 52, 22, 255)
    d.rectangle([0, 0, 15, 15], fill=base)
    # Vach vua xen ke kieu gach xay
    d.line([(0, 4), (15, 4)], fill=mortar)
    d.line([(0, 8), (15, 8)], fill=mortar)
    d.line([(0, 12), (15, 12)], fill=mortar)
    d.line([(8, 0), (8, 4)], fill=mortar)
    d.line([(4, 4), (4, 8)], fill=mortar)
    d.line([(12, 4), (12, 8)], fill=mortar)
    d.line([(8, 8), (8, 12)], fill=mortar)
    d.line([(4, 12), (4, 15)], fill=mortar)
    d.line([(12, 12), (12, 15)], fill=mortar)
    return img


def make_steel():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    base = (146, 150, 160, 255)
    light = (210, 214, 222, 255)
    darkc = (78, 82, 92, 255)
    d.rectangle([0, 0, 15, 15], fill=base)
    d.rectangle([0, 0, 7, 7], fill=light)
    d.rectangle([8, 8, 15, 15], fill=light)
    d.rectangle([0, 8, 7, 15], fill=darkc)
    d.rectangle([8, 0, 15, 7], fill=darkc)
    d.rectangle([0, 0, 15, 15], outline=(40, 40, 46, 255))
    return img


def make_water(frame):
    img = new_canvas()
    d = ImageDraw.Draw(img)
    base = (32, 84, 188, 255)
    wave = (96, 150, 235, 255)
    d.rectangle([0, 0, 15, 15], fill=base)
    offset = 0 if frame == 0 else 4
    for y in (3, 9):
        for x in range(-4 + offset, 16, 8):
            d.line([(x, y), (x + 3, y)], fill=wave, width=1)
    for y in (6, 12):
        for x in range(-4 - offset, 16, 8):
            d.line([(x, y), (x + 3, y)], fill=wave, width=1)
    return img


def make_grass():
    """Nen trong suot, chi ve cum co - khi ve de len tren tank se tao hieu ung an."""
    img = new_canvas()
    d = ImageDraw.Draw(img)
    dark = (24, 92, 24, 235)
    mid = (46, 130, 46, 200)
    tufts = [(1, 15, 3, 4), (4, 15, 6, 2), (7, 15, 9, 5), (10, 15, 12, 3), (13, 15, 15, 6)]
    for x0, y0, x1, y1 in tufts:
        d.line([(x0, y0), (x1, y1)], fill=dark, width=2)
    tufts2 = [(2, 15, 4, 9), (6, 15, 7, 8), (9, 15, 11, 10), (12, 15, 14, 9)]
    for x0, y0, x1, y1 in tufts2:
        d.line([(x0, y0), (x1, y1)], fill=mid, width=2)
    return img


def make_ice():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    base = (196, 230, 248, 255)
    crack = (150, 200, 230, 255)
    d.rectangle([0, 0, 15, 15], fill=base)
    d.line([(2, 2), (8, 9)], fill=crack)
    d.line([(8, 9), (6, 15)], fill=crack)
    d.line([(8, 9), (14, 6)], fill=crack)
    d.rectangle([0, 0, 15, 15], outline=(170, 210, 235, 255))
    return img


def make_base(destroyed=False):
    img = new_canvas()
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 15, 15], fill=(38, 36, 34, 255))
    if destroyed:
        rubble = (70, 60, 55, 255)
        d.rectangle([2, 10, 13, 15], fill=rubble)
        d.line([(3, 10), (6, 4)], fill=(50, 44, 40, 255), width=2)
        d.line([(11, 10), (9, 5)], fill=(50, 44, 40, 255), width=2)
    else:
        gold = (255, 205, 40, 255)
        goldd = (200, 150, 10, 255)
        d.polygon([(8, 1), (14, 14), (2, 14)], fill=gold, outline=goldd)
        d.rectangle([6, 9, 9, 14], fill=goldd)
    return img


def make_bullet():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    d.ellipse([6, 6, 9, 9], fill=(255, 235, 60, 255), outline=(180, 130, 0, 255))
    return img


def main():
    make_tank((60, 130, 235, 255), (20, 60, 140, 255), (40, 40, 46, 255),
              (35, 95, 190, 255), (15, 15, 18, 255), (130, 175, 245, 255)).save("tank_player0.png")
    make_tank((235, 110, 40, 255), (150, 55, 10, 255), (40, 40, 46, 255),
              (190, 80, 25, 255), (15, 15, 18, 255), (250, 165, 110, 255)).save("tank_player1.png")
    make_tank((200, 60, 60, 255), (110, 25, 25, 255), (40, 40, 46, 255),
              (160, 40, 40, 255), (15, 15, 18, 255), (230, 120, 120, 255)).save("tank_enemy.png")
    make_brick().save("brick.png")
    make_steel().save("steel.png")
    make_water(0).save("water1.png")
    make_water(1).save("water2.png")
    make_grass().save("grass.png")
    make_ice().save("ice.png")
    make_base(False).save("base.png")
    make_base(True).save("base_ruin.png")
    make_bullet().save("bullet.png")
    print("Da tao xong toan bo sprite trong thu muc hien tai.")


if __name__ == "__main__":
    main()
