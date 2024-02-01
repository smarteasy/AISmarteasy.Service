//public static class PineconeUtils
//{
//    private static int GetEntrySize(KeyValuePair<string, object> entry)
//    {
//        Dictionary<string, object> temp = new() { { entry.Key, entry.Value } };
//        return GetMetadataSize(temp);
//    }



//    public static string MetricTypeToString(IndexMetric indexMetric)
//    {
//        return indexMetric switch
//        {
//            IndexMetric.Cosine => "cosine",
//            IndexMetric.Dotproduct => "dotProduct",
//            IndexMetric.Euclidean => "euclidean",
//            _ => string.Empty
//        };
//    }

//    public static string PodTypeToString(PodTypeKind podType)
//    {
//        return podType switch
//        {
//            PodTypeKind.P1X1 => "p1x1",
//            PodTypeKind.P1X2 => "p1x2",
//            PodTypeKind.P1X4 => "p1x4",
//            PodTypeKind.P1X8 => "p1x8",
//            PodTypeKind.P2X1 => "p2x1",
//            PodTypeKind.P2X2 => "p2x2",
//            PodTypeKind.P2X4 => "p2x4",
//            PodTypeKind.P2X8 => "p2x8",
//            PodTypeKind.S1X1 => "s1x1",
//            PodTypeKind.S1X2 => "s1x2",
//            PodTypeKind.S1X4 => "s1x4",
//            PodTypeKind.S1X8 => "s1x8",
//            _ => string.Empty
//        };
//    }

//}
