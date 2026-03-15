using System.Text.Json;
using System.Text.Json.Serialization;

namespace TermRTS.Serialization;

public class BooleanArray2DConverter : JsonConverter<bool[,]>
{
    public override bool[,] Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array.");

        reader.Read(); // Move to the first inner array

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of inner array.");

        var rows = new List<bool[]>();
        while (reader.TokenType == JsonTokenType.StartArray)
        {
            var row = new List<bool>();
            reader.Read(); // Move to the first element in the inner array

            while (reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.False &&
                    reader.TokenType != JsonTokenType.True)
                    throw new JsonException("Expected boolean type (true or false).");
                row.Add(reader.GetBoolean());
                reader.Read();
            }

            rows.Add(row.ToArray());
            reader.Read(); // Move to the next inner array or the end of the outer array
        }

        var rowCount = rows.Count;
        if (rowCount == 0) return new bool[0, 0]; // Empty 2D array

        var colCount = rows[0].Length;
        var result = new bool[rowCount, colCount];

        for (var i = 0; i < rowCount; i++)
        {
            if (rows[i].Length != colCount)
                throw new JsonException("Inner arrays must have the same length.");
            for (var j = 0; j < colCount; j++) result[i, j] = rows[i][j];
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, bool[,] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        var rows = value.GetLength(0);
        var cols = value.GetLength(1);

        for (var i = 0; i < rows; i++)
        {
            writer.WriteStartArray();
            for (var j = 0; j < cols; j++) writer.WriteBooleanValue(value[i, j]);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}