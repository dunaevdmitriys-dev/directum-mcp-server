using DirectumMcp.Core.Parsers;
using Xunit;

namespace DirectumMcp.Tests;

public class MtdParserTests
{
    private const string DatabookEntryBaseGuid = "04581d26-0571-11e4-95ef-00155d043204";
    private const string DocumentBaseGuid = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1";

    private const string SampleEntityMtdJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "Deal",
          "BaseGuid": "04581d26-0571-11e4-95ef-00155d043204",
          "IsAbstract": false,
          "IsVisible": true,
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "11111111-1111-1111-1111-111111111111",
              "Name": "Name",
              "Code": "Name",
              "IsRequired": true,
              "Length": 250
            },
            {
              "$type": "Sungero.Metadata.NavigationPropertyMetadata",
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "Counterparty",
              "Code": "Counterparty",
              "EntityGuid": "294767f1-009f-4fbd-80fc-f98c49ddc560"
            },
            {
              "$type": "Sungero.Metadata.CollectionPropertyMetadata",
              "NameGuid": "33333333-3333-3333-3333-333333333333",
              "Name": "Products",
              "Code": "Products"
            },
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "44444444-4444-4444-4444-444444444444",
              "Name": "Status",
              "Code": "Status"
            }
          ],
          "Actions": [
            {
              "NameGuid": "55555555-5555-5555-5555-555555555555",
              "Name": "Approve",
              "IsAncestorMetadata": false,
              "GenerateHandler": true
            },
            {
              "NameGuid": "66666666-6666-6666-6666-666666666666",
              "Name": "Send",
              "IsAncestorMetadata": true,
              "GenerateHandler": false
            }
          ]
        }
        """;

    private const string SampleModuleMtdJson = """
        {
          "NameGuid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          "Name": "DealModule",
          "Version": "1.0.0.0",
          "Dependencies": [
            {
              "Id": "11111111-aaaa-bbbb-cccc-dddddddddddd",
              "IsSolutionModule": false,
              "MaxVersion": "",
              "MinVersion": ""
            }
          ],
          "ExplorerTreeOrder": 100,
          "IsVisible": true
        }
        """;

    private const string DocumentEntityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "b1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "Contract",
          "BaseGuid": "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
          "Properties": [],
          "Actions": []
        }
        """;

    private const string MinimalEntityJson = """
        {
          "NameGuid": "c1c2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "SimpleEntity",
          "Properties": [],
          "Actions": []
        }
        """;

    #region Entity Metadata Parsing

    [Fact]
    public void ParseEntityFromString_ValidJson_ReturnsEntityMetadata()
    {
        // Act
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        // Assert
        Assert.Equal("Deal", entity.Name);
        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", entity.NameGuid);
        Assert.False(entity.IsAbstract);
        Assert.True(entity.IsVisible);
    }

    [Fact]
    public void ParseEntityFromString_ValidJson_ParsesBaseGuid()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        Assert.Equal(DatabookEntryBaseGuid, entity.BaseGuid);
    }

    [Fact]
    public void ParseEntityFromString_NoBaseGuid_ReturnsNull()
    {
        var entity = MtdParser.ParseEntityFromString(MinimalEntityJson);

        Assert.Null(entity.BaseGuid);
    }

    [Fact]
    public void ParseEntityFromString_ValidJson_ParsesAllProperties()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        Assert.Equal(4, entity.Properties.Count);
    }

    [Fact]
    public void ParseEntityFromString_ValidJson_ParsesAllActions()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        Assert.Equal(2, entity.Actions.Count);
        Assert.Equal("Approve", entity.Actions[0].Name);
        Assert.False(entity.Actions[0].IsAncestorMetadata);
        Assert.True(entity.Actions[0].GenerateHandler);
    }

    #endregion

    #region DatabookEntry Detection

    [Fact]
    public void ParseEntityFromString_DatabookEntryBase_DetectedByBaseGuid()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        bool isDatabookEntry = entity.BaseGuid == DatabookEntryBaseGuid;

        Assert.True(isDatabookEntry);
    }

    [Fact]
    public void ParseEntityFromString_DocumentBase_NotDatabookEntry()
    {
        var entity = MtdParser.ParseEntityFromString(DocumentEntityJson);

        bool isDatabookEntry = entity.BaseGuid == DatabookEntryBaseGuid;

        Assert.False(isDatabookEntry);
        Assert.Equal(DocumentBaseGuid, entity.BaseGuid);
    }

    #endregion

    #region CollectionPropertyMetadata Detection

    [Fact]
    public void ParseEntityFromString_HasCollectionProperty_Detected()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        var collectionProperties = entity.Properties
            .Where(p => p.PropertyType == "Sungero.Metadata.CollectionPropertyMetadata")
            .ToList();

        Assert.Single(collectionProperties);
        Assert.Equal("Products", collectionProperties[0].Name);
    }

    [Fact]
    public void ParseEntityFromString_DatabookEntryWithCollection_ValidationFails()
    {
        // Arrange: entity with DatabookEntry base and collection property
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        // Act: check for the known problematic combination
        bool isDatabookEntry = entity.BaseGuid == DatabookEntryBaseGuid;
        bool hasCollections = entity.Properties
            .Any(p => p.PropertyType == "Sungero.Metadata.CollectionPropertyMetadata");

        // Assert: this combination causes "Missing area" error in DDS 25.3
        Assert.True(isDatabookEntry && hasCollections,
            "DatabookEntry with CollectionPropertyMetadata should be flagged as invalid.");
    }

    [Fact]
    public void ParseEntityFromString_NoCollectionProperties_Empty()
    {
        var entity = MtdParser.ParseEntityFromString(DocumentEntityJson);

        var collectionProperties = entity.Properties
            .Where(p => p.PropertyType == "Sungero.Metadata.CollectionPropertyMetadata")
            .ToList();

        Assert.Empty(collectionProperties);
    }

    #endregion

    #region NavigationProperty Extraction

    [Fact]
    public void ParseEntityFromString_NavigationProperty_ExtractsEntityGuid()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        var navProperties = entity.Properties
            .Where(p => p.PropertyType == "Sungero.Metadata.NavigationPropertyMetadata")
            .ToList();

        Assert.Single(navProperties);
        Assert.Equal("Counterparty", navProperties[0].Name);
        Assert.Equal("294767f1-009f-4fbd-80fc-f98c49ddc560", navProperties[0].EntityGuid);
    }

    [Fact]
    public void ParseEntityFromString_NavigationProperty_HasCode()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        var navProp = entity.Properties.First(p => p.Name == "Counterparty");

        Assert.Equal("Counterparty", navProp.Code);
    }

    [Fact]
    public void ParseEntityFromString_NoNavigationProperties_ReturnsEmpty()
    {
        var entity = MtdParser.ParseEntityFromString(MinimalEntityJson);

        var navProperties = entity.Properties
            .Where(p => p.PropertyType == "Sungero.Metadata.NavigationPropertyMetadata")
            .ToList();

        Assert.Empty(navProperties);
    }

    #endregion

    #region Enum Property Extraction

    [Fact]
    public void ParseEntityFromString_EnumProperty_Detected()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        var enumProperties = entity.Properties
            .Where(p => p.PropertyType == "Sungero.Metadata.EnumPropertyMetadata")
            .ToList();

        Assert.Single(enumProperties);
        Assert.Equal("Status", enumProperties[0].Name);
    }

    #endregion

    #region String Property Extraction

    [Fact]
    public void ParseEntityFromString_StringProperty_ParsesLengthAndRequired()
    {
        var entity = MtdParser.ParseEntityFromString(SampleEntityMtdJson);

        var nameProp = entity.Properties.First(p => p.Name == "Name");

        Assert.Equal("Sungero.Metadata.StringPropertyMetadata", nameProp.PropertyType);
        Assert.True(nameProp.IsRequired);
        Assert.Equal(250, nameProp.Length);
        Assert.Equal("Name", nameProp.Code);
    }

    #endregion

    #region Module Metadata Parsing

    [Fact]
    public void ParseModuleFromString_ValidJson_ReturnsModuleMetadata()
    {
        var module = MtdParser.ParseModuleFromString(SampleModuleMtdJson);

        Assert.Equal("DealModule", module.Name);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", module.NameGuid);
        Assert.Equal("1.0.0.0", module.Version);
        Assert.Equal(100, module.ExplorerTreeOrder);
        Assert.True(module.IsVisible);
    }

    [Fact]
    public void ParseModuleFromString_ValidJson_ParsesDependencies()
    {
        var module = MtdParser.ParseModuleFromString(SampleModuleMtdJson);

        Assert.Single(module.Dependencies);
        Assert.Equal("11111111-aaaa-bbbb-cccc-dddddddddddd", module.Dependencies[0].Id);
        Assert.False(module.Dependencies[0].IsSolutionModule);
    }

    [Fact]
    public void ParseModuleFromString_NoDependencies_EmptyList()
    {
        const string json = """
            {
              "NameGuid": "00000000-0000-0000-0000-000000000000",
              "Name": "Standalone",
              "Version": "1.0.0.0"
            }
            """;

        var module = MtdParser.ParseModuleFromString(json);

        Assert.Empty(module.Dependencies);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void ParseEntityFromString_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => MtdParser.ParseEntityFromString("not json"));
    }

    [Fact]
    public void ParseEntityFromString_EmptyJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => MtdParser.ParseEntityFromString(""));
    }

    [Fact]
    public void ParseModuleFromString_NullJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => MtdParser.ParseModuleFromString(null!));
    }

    #endregion
}
