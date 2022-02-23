using UnityEditor.GraphToolsFoundation.Overdrive;
using UnityEditor.ShaderGraph.GraphDelta;
using UnityEditor.ShaderGraph.Registry;
using UnityEditor.ShaderGraph.Registry.Types;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.GraphUI
{
    public class FloatPart : SingleFieldPart<FloatField, float>
    {
        protected override string UXMLTemplateName => "StaticPortParts/FloatPart";
        protected override string FieldName => "sg-float-field";

        public FloatPart(string name, IGraphElementModel model, IModelUI ownerElement, string parentClassName, string portName)
            : base(name, model, ownerElement, parentClassName, portName) { }

        protected override void OnFieldValueChanged(ChangeEvent<float> change)
        {
            if (m_Model is not GraphDataNodeModel graphDataNodeModel) return;
            m_OwnerElement.View.Dispatch(new SetGraphTypeValueCommand(graphDataNodeModel,
                m_PortName,
                GraphType.Length.One,
                GraphType.Height.One,
                change.newValue));
        }

        protected override void UpdatePartFromPortReader(IPortReader reader)
        {
            if (!reader.GetField("c0", out float value)) value = 0;
            m_Field.SetValueWithoutNotify(value);
        }
    }
}
