using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Forms;

public sealed class FormOptionTitleTests
{
    [Fact]
    public void A_name_of_up_to_30_characters_stays_whole()
    {
        var name = new string('a', 30);

        Assert.Equal(new FormOptionTitle(name, IsCut: false), FormOptionTitle.From(name));
        Assert.Equal(new FormOptionTitle("Laura Ríos", IsCut: false), FormOptionTitle.From("  Laura Ríos  "));
    }

    [Fact]
    public void A_name_over_30_characters_is_cut_at_29_with_an_ellipsis()
    {
        var title = FormOptionTitle.From("María Fernanda de los Ángeles Rodríguez");

        Assert.True(title.IsCut);
        Assert.Equal("María Fernanda de los Ángeles…", title.Text);
        Assert.Equal(FormsTextLimits.OptionText, title.Text.Length);
        Assert.Equal(new string('b', 29) + "…", FormOptionTitle.From(new string('b', 31)).Text);
    }

    [Fact]
    public void The_cut_never_splits_a_character_nor_leaves_a_space_before_the_ellipsis()
    {
        var withEmoji = new string('c', 28) + "😀" + "dddd";
        var withSpace = new string('e', 28) + " fghij";

        Assert.Equal(new string('c', 28) + "…", FormOptionTitle.From(withEmoji).Text);
        Assert.Equal(new string('e', 28) + "…", FormOptionTitle.From(withSpace).Text);
    }
}
