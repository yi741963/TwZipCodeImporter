-- SQLite 版本的 Zone3Plus3 結構 (參考用,程式啟動時自動建立)
CREATE TABLE IF NOT EXISTS Zone3Plus3 (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    CityName         TEXT    NOT NULL,
    AreaName         TEXT    NOT NULL,
    Code6            INTEGER NOT NULL,
    RoadName         TEXT    NOT NULL,
    DeliveryRangeRaw TEXT    NOT NULL,
    OddEven          TEXT    NOT NULL,
    MinNo            INTEGER NULL,
    MaxNo            INTEGER NULL,
    PostOffice       TEXT    NULL,
    BulkNote         TEXT    NULL,
    IsEnable         INTEGER NOT NULL DEFAULT 1,
    UpdatedAt        TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_Zone3Plus3_Natural
    ON Zone3Plus3 (CityName, AreaName, RoadName, Code6, DeliveryRangeRaw);

CREATE INDEX IF NOT EXISTS IX_Zone3Plus3_Code6
    ON Zone3Plus3 (Code6);

CREATE INDEX IF NOT EXISTS IX_Zone3Plus3_Address
    ON Zone3Plus3 (CityName, AreaName, RoadName);
