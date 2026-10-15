using NSLocker.Ui.ViewModels;

namespace NSLocker.Ui.Controls;

public sealed partial class StateChip : UserControl
{
    public StateChip() => InitializeComponent();

    public ProtocolVm Vm
    {
        get => (ProtocolVm)GetValue(VmProperty);
        set => SetValue(VmProperty, value);
    }

    public static readonly DependencyProperty VmProperty =
        DependencyProperty.Register(nameof(Vm), typeof(ProtocolVm), typeof(StateChip),
            new PropertyMetadata(null, (d, _) => ((StateChip)d).Bindings.Update()));
}
