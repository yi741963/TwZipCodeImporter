-- 參考用：ZoneImporter.cs 中動態執行的 MERGE 語句
-- 實際執行時以 #Stage 暫存表為來源

DECLARE @actions TABLE (act nvarchar(10));

MERGE [tms].[Zone3Plus3] WITH (HOLDLOCK) AS T
USING #Stage AS S
  ON  T.CityName         = S.CityName
  AND T.AreaName          = S.AreaName
  AND T.RoadName          = S.RoadName
  AND T.Code6             = S.Code6
  AND T.DeliveryRangeRaw  = S.DeliveryRangeRaw
WHEN MATCHED THEN
    UPDATE SET
        T.OddEven     = S.OddEven,
        T.MinNo       = S.MinNo,
        T.MaxNo       = S.MaxNo,
        T.PostOffice  = S.PostOffice,
        T.BulkNote    = S.BulkNote,
        T.UpdatedAt   = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (CityName, AreaName, Code6, RoadName, DeliveryRangeRaw,
            OddEven, MinNo, MaxNo, PostOffice, BulkNote, IsEnable, UpdatedAt)
    VALUES (S.CityName, S.AreaName, S.Code6, S.RoadName, S.DeliveryRangeRaw,
            S.OddEven, S.MinNo, S.MaxNo, S.PostOffice, S.BulkNote, 1,
            SYSUTCDATETIME())
OUTPUT $action INTO @actions;

SELECT act, COUNT(*) AS cnt FROM @actions GROUP BY act;
