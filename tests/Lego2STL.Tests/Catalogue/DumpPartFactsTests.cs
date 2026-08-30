using FluentAssertions;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Reading what the dump knows about a part, and staying quiet when it knows nothing.
/// </summary>
/// <remarks>
/// The dump is optional and is not ours to redistribute, so every test here builds its own
/// two-file fixture. The behaviour that matters most is the absence case: a run with no dump,
/// or with a dump missing the column, has to carry on exactly as it did before.
/// </remarks>
public sealed class DumpPartFactsTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-dump-" + Guid.NewGuid().ToString("N"));

    public DumpPartFactsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteDump(string partsBody, string categoriesBody = "id,name\n65,Electronics\n27,Tubes and Hoses\n")
    {
        File.WriteAllText(Path.Combine(_folder, "parts.csv"), partsBody.ReplaceLineEndings());
        File.WriteAllText(Path.Combine(_folder, "part_categories.csv"), categoriesBody.ReplaceLineEndings());
    }

    [Fact]
    public void A_part_in_the_dump_brings_its_kind_and_its_material()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n" +
                  "5102c13,\"Hose, Pneumatic 4mm D. 13L\",27,Rubber\n" +
                  "22127,Hub Powered Up,65,Plastic\n");

        var facts = RebrickableDump.TryReadPartFacts(_folder);

        facts["5102c13"].Should().Be(new PartFact("Tubes and Hoses", "Rubber"));
        facts["22127"].Should().Be(new PartFact("Electronics", "Plastic"));
    }

    /// <summary>Part numbers are matched however they were typed, as everywhere else.</summary>
    [Fact]
    public void A_part_is_found_whatever_case_it_is_asked_for_in()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n4265C,Bush,27,Plastic\n");

        RebrickableDump.TryReadPartFacts(_folder).ContainsKey("4265c").Should().BeTrue();
    }

    [Fact]
    public void A_part_the_dump_has_never_heard_of_is_simply_absent()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n");

        RebrickableDump.TryReadPartFacts(_folder).ContainsKey("99999").Should().BeFalse();
    }

    [Fact]
    public void No_dump_at_all_is_no_facts_and_no_complaint()
    {
        RebrickableDump.TryReadPartFacts(Path.Combine(_folder, "nowhere")).Should().BeEmpty();
        RebrickableDump.TryReadPartFacts(null).Should().BeEmpty();
    }

    /// <summary>A dump whose shape has changed is treated as no dump, never as a reason to stop.</summary>
    [Fact]
    public void A_parts_file_without_the_columns_is_treated_as_no_dump()
    {
        WriteDump("part_num,name\n3001,Brick 2x4\n");

        RebrickableDump.TryReadPartFacts(_folder).Should().BeEmpty();
    }

    /// <summary>
    /// The setting may name one file of the dump rather than the folder, because that is what
    /// it was for before this: the others are found beside it.
    /// </summary>
    [Fact]
    public void Pointing_at_one_file_of_the_dump_finds_the_others_beside_it()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n",
            "id,name\n11,Bricks\n");
        var elements = Path.Combine(_folder, "elements.csv");
        File.WriteAllText(elements, "element_id,part_num,color_id\n300126,3001,4\n");

        RebrickableDump.TryReadPartFacts(elements).Should().ContainKey("3001");
    }

    /// <summary>The first candidate that answers wins, so the setting beats the working folder.</summary>
    [Fact]
    public void The_first_candidate_that_answers_is_the_one_used()
    {
        WriteDump("part_num,name,part_cat_id,part_material\n3001,Brick 2x4,11,Plastic\n",
            "id,name\n11,Bricks\n");

        RebrickableDump.TryReadPartFacts(null, "nowhere", _folder).Should().ContainKey("3001");
    }
}
