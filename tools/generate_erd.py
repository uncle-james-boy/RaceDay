from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


REPO = Path(__file__).resolve().parents[1]
OUT = REPO / "docs" / "Part1_ERD.png"
S = 2
W, H = 1000 * S, 1085 * S
BLUE = (39, 92, 159)
INK = (31, 41, 55)
LINE = (91, 108, 129)
PALE = (247, 249, 252)
ORANGE = (185, 112, 10)
WHITE = (255, 255, 255)


def font(size: int, bold: bool = False):
    name = "arialbd.ttf" if bold else "arial.ttf"
    candidates = [
        Path(r"C:\Windows\Fonts") / name,
        Path("/usr/share/fonts/truetype/msttcorefonts") / name,
        Path("/usr/share/fonts/truetype/dejavu") / ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"),
    ]
    path = next((candidate for candidate in candidates if candidate.exists()), None)
    return ImageFont.truetype(str(path), size * S) if path else ImageFont.load_default()


def xy(x: int, y: int):
    return x * S, y * S


im = Image.new("RGB", (W, H), WHITE)
d = ImageDraw.Draw(im)
regular = font(12)
constraint = font(10)
heading = font(17, True)
title = font(24, True)
small_bold = font(11, True)

boxes = {
    "Users": (40, 105, 280),
    "Events": (360, 105, 290),
    "Categories": (690, 105, 280),
    "Results": (40, 650, 280),
    "EventRoutes": (360, 650, 290),
    "Enrolments": (690, 650, 280),
}

fields = {
    "Users": [
        ("UserID", "int", "PK, IDENTITY"),
        ("FullName", "varchar(100)", "NOT NULL"),
        ("Email", "varchar(150)", "NOT NULL, UNIQUE"),
        ("PasswordHash", "varchar(255)", "NOT NULL"),
        ("Role", "varchar(20)", "NOT NULL, CHECK"),
        ("ProfilePictureUrl", "varchar(500)", "NULL"),
        ("CreatedAt", "datetime", "NOT NULL, DEFAULT"),
    ],
    "Events": [
        ("EventID", "int", "PK, IDENTITY"),
        ("OrganiserID", "int", "FK, NOT NULL"),
        ("Name", "varchar(150)", "NOT NULL"),
        ("Description", "varchar(max)", "NULL"),
        ("EventDate", "datetime", "NOT NULL"),
        ("Location", "varchar(200)", "NOT NULL"),
        ("Distance", "decimal(5,2)", "NOT NULL, CHECK > 0"),
        ("EventType", "varchar(20)", "NOT NULL, CHECK"),
        ("EventBannerUrl", "varchar(500)", "NULL"),
        ("CreatedAt", "datetime", "NOT NULL, DEFAULT"),
    ],
    "Categories": [
        ("CategoryID", "int", "PK, IDENTITY"),
        ("EventID", "int", "FK, NOT NULL"),
        ("CategoryName", "varchar(100)", "NOT NULL"),
        ("MinAge", "int", "NULL, CHECK >= 0"),
        ("MaxAge", "int", "NULL, CHECK >= MinAge"),
        ("Distance", "decimal(5,2)", "NULL, CHECK > 0"),
        ("CategoryID + EventID", "", "UNIQUE alternate key"),
    ],
    "EventRoutes": [
        ("RouteID", "int", "PK, IDENTITY"),
        ("EventID", "int", "FK, NOT NULL"),
        ("RouteName", "varchar(100)", "NOT NULL"),
        ("Distance", "decimal(5,2)", "NOT NULL, CHECK > 0"),
        ("RouteDescription", "varchar(max)", "NULL"),
        ("RouteMapUrl", "varchar(500)", "NULL"),
        ("EventID + RouteName", "", "UNIQUE"),
    ],
    "Enrolments": [
        ("EnrolmentID", "int", "PK, IDENTITY"),
        ("EventID", "int", "FK, NOT NULL"),
        ("ParticipantID", "int", "FK, NOT NULL"),
        ("CategoryID", "int", "COMPOSITE FK"),
        ("EnrolmentDate", "datetime", "NOT NULL, DEFAULT"),
        ("Status", "varchar(20)", "NOT NULL, DEFAULT, CHECK"),
        ("ParticipantID + EventID + CategoryID", "", "UNIQUE"),
    ],
    "Results": [
        ("ResultID", "int", "PK, IDENTITY"),
        ("EnrolmentID", "int", "FK, UNIQUE"),
        ("FinishTime", "time(0)", "NOT NULL"),
        ("Position", "int", "NOT NULL, CHECK > 0"),
        ("CapturedAt", "datetime", "NOT NULL, DEFAULT"),
    ],
}


def put_text(x: int, y: int, value: str, face, color=INK):
    d.text(xy(x, y), value, font=face, fill=color)


def card(name: str):
    x, y, w = boxes[name]
    rows = fields[name]
    h = 51 + len(rows) * 38
    d.rounded_rectangle((x * S, y * S, (x + w) * S, (y + h) * S),
                        radius=9 * S, fill=PALE, outline=(194, 207, 224), width=2 * S)
    d.rounded_rectangle((x * S, y * S, (x + w) * S, (y + 37) * S),
                        radius=9 * S, fill=BLUE)
    d.rectangle((x * S, (y + 25) * S, (x + w) * S, (y + 37) * S), fill=BLUE)
    put_text(x + 12, y + 7, name, heading, WHITE)
    row_y = y + 45
    for key, datatype, flags in rows:
        put_text(x + 12, row_y, key, small_bold)
        if datatype:
            put_text(x + 132, row_y, datatype, regular)
        put_text(x + 12, row_y + 18, flags, constraint, (78, 94, 114))
        row_y += 38


def line(points, color=LINE, dashed=False):
    ps = [xy(*p) for p in points]
    for (x1, y1), (x2, y2) in zip(ps, ps[1:]):
        if not dashed:
            d.line((x1, y1, x2, y2), fill=color, width=2 * S)
            continue
        length = max(abs(x2 - x1), abs(y2 - y1))
        segments = max(1, length // (10 * S))
        for i in range(0, segments, 2):
            a = i / segments
            b = min(i + 1, segments) / segments
            d.line((int(x1 + (x2 - x1) * a), int(y1 + (y2 - y1) * a),
                    int(x1 + (x2 - x1) * b), int(y1 + (y2 - y1) * b)),
                   fill=color, width=2 * S)


annotations = []


def relation(points, label, label_at, ends, end_at, color=LINE, dashed=False):
    line(points, color, dashed)
    annotations.append((label, label_at, ends, end_at, color))


# Relationships sit behind entity boxes. Cardinalities appear at both ends.
relation([(320, 190), (360, 190)], "", (0, 0), ("1", "N"), ((321, 201), (344, 201)))
relation([(650, 225), (690, 225)], "", (0, 0), ("1", "N"), ((651, 236), (674, 236)))
relation([(500, 536), (500, 650)], "", (0, 0), ("1", "N"), ((505, 538), (505, 628)))
relation([(830, 422), (830, 650)], "composite Category/Event FK", (842, 535),
         ("1", "N"), ((835, 424), (835, 628)), color=ORANGE, dashed=True)
relation([(650, 460), (670, 460), (670, 735), (690, 735)], "", (0, 0),
         ("1", "N"), ((652, 465), (666, 735)))
relation([(300, 422), (335, 422), (335, 600), (675, 600), (675, 790), (690, 790)],
         "", (0, 0), ("1", "N"), ((302, 425), (665, 790)))
relation([(735, 967), (735, 985), (180, 985), (180, 891)], "Enrolment has optional result",
         (390, 972), ("1", "0..1"), ((900, 970), (164, 894)))

put_text(40, 25, "RaceDay relational model", title)
put_text(40, 65, "PK primary key   FK foreign key   UNIQUE alternate/unique key   CHECK / DEFAULT constraints shown",
         regular, (70, 84, 102))

for entity in boxes:
    card(entity)

for text, at, ends, end_at, color in annotations:
    if text:
        # White backing keeps captions easy to read where a line passes nearby.
        bounds = d.textbbox(xy(*at), text, font=small_bold)
        d.rounded_rectangle((bounds[0] - 6 * S, bounds[1] - 2 * S,
                             bounds[2] + 6 * S, bounds[3] + 2 * S),
                            radius=3 * S, fill=WHITE)
        put_text(*at, text, small_bold, color)
    for value, location in zip(ends, end_at):
        mark_bounds = d.textbbox(xy(*location), value, font=small_bold)
        d.rounded_rectangle((mark_bounds[0] - 3 * S, mark_bounds[1] - S,
                             mark_bounds[2] + 3 * S, mark_bounds[3] + S),
                            radius=3 * S, fill=WHITE)
        put_text(*location, value, small_bold, color)

put_text(40, 1000, "Relationships: User (Organiser) 1:N Events; Event 1:N Categories and EventRoutes.",
         regular, (70, 84, 102))
put_text(40, 1018, "User (Participant) 1:N Enrolments; Event 1:N Enrolments; Category 1:N Enrolments.",
         regular, (70, 84, 102))
put_text(40, 1036, "Enrolment 1:0..1 Result. N = many; 0..1 = optional one. Orange dashed = composite CategoryID + EventID FK.",
         regular, ORANGE)

OUT.parent.mkdir(parents=True, exist_ok=True)
im.save(OUT, format="PNG", optimize=True)
print(f"Generated {OUT}")
