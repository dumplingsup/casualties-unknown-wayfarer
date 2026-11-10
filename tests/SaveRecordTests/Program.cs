using System;
using Newtonsoft.Json.Linq;
using WayfarerMech;

var input = JObject.Parse("{\"schemaVersion\":9,\"future\":{\"parts\":[1,2,3]},\"body\":\"unavailable\"}");
var expected = input.DeepClone();
var record = new PreservedLoadoutRecord(input, 7);
input["future"]["parts"][0] = 99;
Check(JToken.DeepEquals(record.Capture(), expected), "restore owns its snapshot");
Check(record.Version == 7, "future envelope version retained");
var captured = record.Capture();
captured["future"]["parts"][1] = 99;
Check(JToken.DeepEquals(record.Capture(), expected), "capture cannot mutate next save");
foreach (var raw in new JToken[] { new JValue("malformed"), new JArray(1, 2), JValue.CreateNull() })
    Check(JToken.DeepEquals(new PreservedLoadoutRecord(raw, 1).Capture(), raw), "malformed payload retained");
Console.WriteLine("ALL SAVE RECORD TESTS PASSED");
static void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}
