"""Cross-check the Part 1 planning artifacts without connecting to SQL Server."""

from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[1]
EXPECTED_TABLES = {"Users", "Events", "Categories", "EventRoutes", "Enrolments", "Results"}


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


dbml = read("schema.dbml")
sql = read("docs/RaceDay_Database.sql")
plan = read("docs/API_Endpoint_Plan.md")
blueprint = read("docs/Part2_Implementation_Blueprint.md")
readme = read("README.md")
errors: list[str] = []

dbml_tables = set(re.findall(r"(?m)^Table\s+(\w+)\s*\{", dbml))
sql_tables = set(re.findall(r"(?im)^\s*CREATE\s+TABLE\s+dbo\.(\w+)", sql))

if dbml_tables != EXPECTED_TABLES:
    errors.append(f"DBML entity list is not the approved six: {sorted(dbml_tables)}")
if sql_tables != EXPECTED_TABLES:
    errors.append(f"SQL tables do not match DBML: {sorted(sql_tables)}")
if "EventRoutes" not in readme or "EventRoutes" not in blueprint or "EventRoutes" not in plan:
    errors.append("README, endpoint plan, and Part 2 blueprint must all describe EventRoutes")

required_header = "| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |"
if required_header not in plan:
    errors.append("Endpoint plan must use the six columns required by Part 1")

for required in [
    "FOREIGN KEY (CategoryID, EventID)",
    "REFERENCES dbo.Categories(CategoryID, EventID)",
    "CREATE DATABASE RaceDayDB",
    "USE RaceDayDB",
    "THROW 50001",
    "UQ_EventRoutes_EventID_RouteName",
]:
    if required not in sql:
        errors.append(f"SQL is missing required safe setup/model text: {required}")
if re.search(r"(?i)\bDROP\s+TABLE\b", sql):
    errors.append("SQL setup must never drop existing tables")
if "returns jwt" in plan.lower():
    errors.append("Endpoint plan must describe server-side sessions, not JWT login")

required_routes = [
    "/api/events/{eventId}",
    "/api/categories/{categoryId}",
    "/api/events/{eventId}/routes",
    "/api/routes/{routeId}",
    "/api/enrolments",
    "/api/events/{eventId}/enrolments",
    "/api/results",
]
for route in required_routes:
    if route not in plan:
        errors.append(f"Endpoint plan is missing {route}")

minimum_seed_rows = {
    "Users": 5,
    "Events": 4,
    "Categories": 8,
    "EventRoutes": 8,
    "Enrolments": 7,
    "Results": 3,
}
for table, minimum in minimum_seed_rows.items():
    match = re.search(rf"INSERT\s+INTO\s+dbo\.{table}\b.*?VALUES(.*?);", sql, re.I | re.S)
    row_count = len(re.findall(r"(?m)^\s*\(", match.group(1))) if match else 0
    if row_count < minimum:
        errors.append(f"Expected at least {minimum} seed rows for {table}; found {row_count}")

if not (ROOT / "docs/Part1_ERD.png").is_file():
    errors.append("ERD PNG is missing")
renderer_path = ROOT / "tools/generate_erd.py"
if not renderer_path.is_file():
    errors.append("ERD source renderer is missing")
else:
    renderer = renderer_path.read_text(encoding="utf-8-sig")
    for entity in EXPECTED_TABLES:
        if entity not in renderer:
            errors.append(f"ERD renderer does not describe {entity}")

try:
    from PIL import Image
    erd_path = ROOT / "docs/Part1_ERD.png"
    with Image.open(erd_path) as erd:
        if erd.format != "PNG" or erd.width < 1400 or erd.height < 1400:
            errors.append("ERD PNG is missing, invalid, or too small to read")
        erd.verify()
except (ImportError, OSError) as error:
    errors.append(f"ERD image validation failed: {error}")

if errors:
    print("Part 1 validation failed:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Part 1 validation passed: six entities, matching SQL tables, constraints, seed data, and endpoint plan.")
