namespace Lumper.UI.ViewModels.Shared.Entity;

using System;
using System.Collections.Generic;
using System.Globalization;
using Lumper.Lib.Bsp.Struct;
using Lumper.Lib.ExtensionMethods;
using Lumper.Lib.FGD;
using Lumper.UI.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

public abstract class EntityPropertyViewModel : HierarchicalBspNode
{
    public Entity.EntityProperty Property { get; }
    public EntityViewModel ParentEntity { get; }

    // Note that EntityPropertyViewModels all store copies of their corresponding model values;
    // without we'd be unable to tell if a viewmodel has been modified when a job or other
    // Lumper.Lib code is ran that updates the model. When this happens, calling PullChangesFromModel
    // can simply try setting the viewmodel value to model value, and only RaisePropertyChanged
    // if it changed.
    private string _key;
    public string Key
    {
        get => _key;
        set
        {
            bool wasClassname = _key == "classname";

            if (_key == value)
                return;

            _key = value;
            Property.Key = value;
            this.RaisePropertyChanged();

            if (this is not EntityPropertyStringViewModel vm)
                return;

            if (wasClassname)
                ((EntityViewModel)Parent).ResetClassname();
            else if (value == "classname")
                ((EntityViewModel)Parent).Classname = vm.Value;
        }
    }

    protected EntityPropertyViewModel(Entity.EntityProperty property, BspNode bspNode)
    : base(bspNode)
    {
        Property = property;
        ParentEntity = (EntityViewModel)bspNode;
        _key = property.Key;

        this.WhenAnyValue(x => x.ParentEntity.Classname)
            .Subscribe(classname => KeySuggestions = FetchKeySuggestionsForClassname(classname));
    }

    [Reactive]
    public IReadOnlyCollection<ExtendedAutoCompleteItem> KeySuggestions { get; set; } = [];

    public List<ExtendedAutoCompleteItem> FetchKeySuggestionsForClassname(string classname)
    {
        List<ExtendedAutoCompleteItem> suggestions = [];
        if (!string.IsNullOrWhiteSpace(classname) && MomentumFGD.Entities.TryGetValue(classname, out FGDEntity? fgdEntity))
        {
            foreach (FGDProperty prop in fgdEntity.Properties.Values)
            {
                var item = new ExtendedAutoCompleteItem
                {
                    Value = prop.Name,
                };

                suggestions.Add(item);
            }
        }
        return suggestions;
    }

    public virtual void OnClassnameChanged() { }

    public static EntityPropertyViewModel Create(Entity.EntityProperty entityProperty, EntityViewModel parent)
    {
        return entityProperty switch
        {
            Entity.EntityProperty<string> sp => new EntityPropertyStringViewModel(sp, parent),
            Entity.EntityProperty<EntityIo> sio => new EntityPropertyIoViewModel(sio, parent),
            _ => throw new ArgumentOutOfRangeException(nameof(entityProperty)),
        };
    }

    public abstract bool MemberwiseEquals(EntityPropertyViewModel other);

    public bool MatchKey(string expr, bool wildcardWrapping)
    {
        return Key.MatchesSimpleExpression(expr, wildcardWrapping);
    }

    public abstract bool MatchValue(string expr, bool wildcardWrapping);

    public void Delete()
    {
        ((EntityViewModel)Parent).DeleteProperty(this);
    }
}

public class EntityPropertyStringViewModel : EntityPropertyViewModel
{
    private readonly Entity.EntityProperty<string> _property;
    private string _value;

    public EntityPropertyStringViewModel(Entity.EntityProperty<string> property, BspNode bspNode)
        : base(property, bspNode)
    {
        _property = property;
        _value = property.Value;

        this.WhenAnyValue(
            x => x.Key,
            x => x.ParentEntity.Classname)
            .Subscribe(_ =>
            {
                ValueSuggestions = FetchValueSuggestionsForKey();
                IsBitfield = IsKeyBitfield();
            });
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;

            _value = value;
            _property.Value = value;
            MarkAsModified();
            this.RaisePropertyChanged();

            if (Key == "classname")
                ((EntityViewModel)Parent).Classname = value;
        }
    }

    [Reactive]
    public IReadOnlyCollection<ExtendedAutoCompleteItem> ValueSuggestions { get; set; } = [];

    [Reactive]
    public bool IsBitfield { get; set; } = false;

    private bool IsKeyBitfield()
    {
        if (MomentumFGD.Entities.TryGetValue(ParentEntity.Classname, out FGDEntity? fgdEntity)
            && fgdEntity.Properties.TryGetValue(Key, out FGDProperty? fgdProp))
        {
            return fgdProp.ValueType == "flags";
        }
        return false;
    }

    private List<ExtendedAutoCompleteItem> FetchValueSuggestionsForKey()
    {
        List<ExtendedAutoCompleteItem> suggestions = [];

        if (MomentumFGD.Entities.TryGetValue(ParentEntity.Classname, out FGDEntity? fgdEntity)
            && fgdEntity.Properties.TryGetValue(Key, out FGDProperty? fgdProp))
        {
            IReadOnlyDictionary<string, string> choices = fgdProp.Choices;

            foreach (KeyValuePair<string, string> choice in choices)
            {
                var item = new ExtendedAutoCompleteItem
                {
                    Value = choice.Key,
                    Display = fgdProp.ValueType == "flags" ? choice.Value : $"[{choice.Key}] {choice.Value}"
                };
                suggestions.Add(item);
            }
        }
        return suggestions;
    }

    public override bool MemberwiseEquals(EntityPropertyViewModel other)
    {
        return other is EntityPropertyStringViewModel o && o.Key == Key && o.Value == Value;
    }

    public override bool MatchValue(string expr, bool wildcardWrapping)
    {
        return Value.MatchesSimpleExpression(expr, wildcardWrapping);
    }
}

// ReSharper disable CompareOfFloatsByEqualityOperator
public class EntityPropertyIoViewModel : EntityPropertyViewModel
{
    private readonly Entity.EntityProperty<EntityIo> _property;
    private string _ioKey;
    private IReadOnlyCollection<ExtendedAutoCompleteItem> _ioKeySuggestions = [];
    private string _targetEntityName;
    private string _input;
    private string _parameter;
    private float _delay;
    private int _timesToFire;
    public EntityPropertyIoViewModel(Entity.EntityProperty<EntityIo> property, BspNode bspNode)
        : base(property, bspNode)
    {
        _property = property;
        _ioKey = property.Key;
        _targetEntityName = property.Value.TargetEntityName;
        _input = property.Value.Input;
        _parameter = property.Value.Parameter;
        _delay = property.Value.Delay;
        _timesToFire = property.Value.TimesToFire;

        this.WhenAnyValue(x => x.ParentEntity.Classname)
            .Subscribe(_ => IOKeySuggestions = FetchIoKeySuggestions());
    }

    public string IOKey
    {
        get => _ioKey;
        set
        {
            _ioKey = value;
            OnValueChanged();
        }
    }

    public IReadOnlyCollection<ExtendedAutoCompleteItem> IOKeySuggestions
    {
        get => _ioKeySuggestions;
        set
        {
            _ioKeySuggestions = value;
            OnValueChanged();
        }
    }

    public string TargetEntityName
    {
        get => _targetEntityName;
        set
        {
            if (_targetEntityName == value)
                return;

            _targetEntityName = value;
            _property.Value.TargetEntityName = value;
            this.RaisePropertyChanged();
            OnValueChanged();
        }
    }
    public string Input
    {
        get => _input;
        set
        {
            if (_input == value)
                return;

            _input = value;
            _property.Value.Input = value;
            this.RaisePropertyChanged();
            OnValueChanged();
        }
    }

    public string Parameter
    {
        get => _parameter;
        set
        {
            if (_parameter == value)
                return;

            _parameter = value;
            _property.Value.Parameter = value;
            this.RaisePropertyChanged();
            OnValueChanged();
        }
    }
  
    public float Delay
    {
        get => _delay;
        set
        {
            if (_delay == value)
                return;

            _delay = value;
            _property.Value.Delay = value;
            this.RaisePropertyChanged();
            OnValueChanged();
        }
    }

    public int TimesToFire
    {
        get => _timesToFire;
        set
        {
            if (_timesToFire == value)
                return;

            _timesToFire = value;
            _property.Value.TimesToFire = value;
            this.RaisePropertyChanged();
            OnValueChanged();
        }
    }

    public string DisplayValue => _property.Value.ToString();

    private void OnValueChanged()
    {
        MarkAsModified();
        this.RaisePropertyChanged(nameof(DisplayValue));
    }

    private List<ExtendedAutoCompleteItem> FetchIoKeySuggestions()
    {
        List<ExtendedAutoCompleteItem> suggestions = [];

        if (Parent is EntityViewModel entityVm
            && MomentumFGD.Entities.TryGetValue(entityVm.Classname, out FGDEntity? fgdEntity))
        {
            foreach (KeyValuePair<string, FGDOutput> output in fgdEntity.Outputs)
            {
                var item = new ExtendedAutoCompleteItem
                {
                    Value = output.Value.Name,
                };
                suggestions.Add(item);
            }
        }
        return suggestions;
    }

    public override bool MemberwiseEquals(EntityPropertyViewModel other)
    {
        return other is EntityPropertyIoViewModel o
            && o.Key == Key
            && o.TargetEntityName == TargetEntityName
            && o.Input == Input
            && o.Parameter == Parameter
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            && o.Delay == Delay
            && o.TimesToFire == TimesToFire;
    }

    public override bool MatchValue(string expr, bool wildcardWrapping)
    {
        // Match against both comma and space separated values
        return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{TargetEntityName} {Input} {Parameter} {Delay} {TimesToFire}"
                )
                .MatchesSimpleExpression(expr, wildcardWrapping)
            || string.Create(
                    CultureInfo.InvariantCulture,
                    $"{TargetEntityName},{Input},{Parameter},{Delay},{TimesToFire}"
                )
                .MatchesSimpleExpression(expr, wildcardWrapping);
    }
}
