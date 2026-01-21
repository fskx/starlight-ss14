using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

public sealed class IconTag : IMarkupTag
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    private SpriteSystem? _spriteSystem;

    public string Name => "icon";

    public bool TryGetControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Attributes.TryGetValue("src", out var id) || id.StringValue == null)
        {
            control = null;
            return false;
        }
        _spriteSystem ??= _entitySystem.GetEntitySystem<SpriteSystem>();
        /* Starlight */
        AnimatedTextureRect? animated = null;
        TextureRect? icon = null;

        JobIconPrototype? jobProto = null;
        EmojiIconPrototype? emojiProto = null;
        if (!_prototype.TryIndex<JobIconPrototype>(id.StringValue, out jobProto))
        {
            _prototype.TryIndex<EmojiIconPrototype>(id.StringValue, out emojiProto);
        }

        if (jobProto != null || emojiProto != null)
        {
            var spec = jobProto != null ? jobProto.Icon : emojiProto!.Icon;
            try
            {
                var state = _spriteSystem.RsiStateLike(spec);
                if (state.IsAnimated)
                {
                    var anim = new AnimatedTextureRect();
                    anim.SetFromSpriteSpecifier(spec);
                    anim.SetWidth = 20f;
                    anim.SetHeight = 20f;
                    anim.MouseFilter = Control.MouseFilterMode.Stop;
                    animated = anim;
                }
                else
                {
                    var texture = _spriteSystem.Frame0(spec);
                    icon = new TextureRect
                    {
                        Texture = texture,
                        SetWidth = 20,
                        SetHeight = 20,
                        Stretch = TextureRect.StretchMode.Scale,
                        MouseFilter = Control.MouseFilterMode.Stop,
                    };
                }
            }
            catch
            {
                // If anything goes wrong, fall back to texture
                var texture = _spriteSystem.Frame0(spec);
                icon = new TextureRect
                {
                    Texture = texture,
                    SetWidth = 20,
                    SetHeight = 20,
                    Stretch = TextureRect.StretchMode.Scale,
                    MouseFilter = Control.MouseFilterMode.Stop,
                };
            }
        }
        if (node.Attributes.TryGetValue("tooltip", out var tooltip) && tooltip.StringValue != null)
        {
            if (animated != null)
                animated.ToolTip = tooltip.StringValue;
            else if (icon != null)
                icon.ToolTip = tooltip.StringValue;
        }

        control = (Control?)animated ?? icon;
        return control != null;
    }
}
