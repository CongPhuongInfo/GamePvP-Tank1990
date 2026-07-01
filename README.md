# Tank1990Online
Game Tank 1990 (Battle City) qua mang LAN hoac Online - phien ban 2CongLC

Che do:
- PvP: 2 nguoi choi doi dau qua TCP LAN, moi nguoi co can cu rieng can bao ve
- PvAI: choi mot minh, bao ve can cu va tieu diet du so xe dich de thang

Dieu khien: WASD / Mui ten de di chuyen va xoay huong, Space de ban.

## Build
Chay `build_tank1990.bat` tren may co cai .NET Framework 4.x (can vbc.exe).
Sau khi build xong, **nho copy thu muc `sprites/` vao chung thu muc voi file
.exe vua tao** - day la sprite pixel-art (PNG) tu thiet ke, khong dung asset
ban quyen cua Battle City/Namco. Neu thieu thu muc sprites, game van chay
binh thuong nhung se tu dong fallback ve bang hinh hoc GDI+ (hinh vuong/tron
don gian) thay vi sprite.

Muon tu sinh lai sprite (vi du doi mau, doi kich thuoc): chay
`python3 sprites/gen_sprites.py` (can cai thu vien Pillow: `pip install pillow`).
