using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEditor.Graphing.Util;
using UnityEngine.Graphing;
using UnityEngine.MaterialGraph;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace UnityEditor.MaterialGraph.Drawing
{
    public class MaterialNodeView : Node
    {
        VisualElement m_ControlsContainer;
        List<GraphControlPresenter> m_CurrentControls;
        VisualElement m_PreviewContainer;
        Image m_PreviewImage;
        bool m_IsScheduled;

        public MaterialNodeView()
        {
            CreateContainers();

            AddToClassList("MaterialNode");
        }

        void CreateContainers()
        {
            m_ControlsContainer = new VisualElement
            {
                name = "controls"
            };
            leftContainer.Add(m_ControlsContainer);
            m_CurrentControls = new List<GraphControlPresenter>();

            m_PreviewContainer = new VisualElement { name = "preview" };
            {
                m_PreviewImage = new Image
                {
                    pickingMode = PickingMode.Ignore,
                    image = Texture2D.whiteTexture
                };
            }
            leftContainer.Add(m_PreviewContainer);
        }

        void UpdatePreviewTexture(NodePreviewPresenter preview)
        {
            if (preview != null)
                preview.UpdateTexture();
            if (preview == null || preview.texture == null)
            {
//                Debug.Log(GetPresenter<MaterialNodePresenter>().node.name);
                m_PreviewContainer.Clear();
                m_PreviewImage.image = Texture2D.whiteTexture;
            }
            else
            {
                if (m_PreviewContainer.childCount == 0)
                    m_PreviewContainer.Add(m_PreviewImage);
                m_PreviewImage.image = preview.texture;
            }
//            Dirty(ChangeType.Layout);
        }

        void UpdateControls(MaterialNodePresenter nodeData)
        {
            if (nodeData.controls.SequenceEqual(m_CurrentControls) && nodeData.expanded)
                return;

            m_ControlsContainer.Clear();
            m_CurrentControls.Clear();

            if (!nodeData.expanded)
                return;

            foreach (var controlData in nodeData.controls)
            {
                m_ControlsContainer.Add(new IMGUIContainer(controlData.OnGUIHandler)
                {
                    name = "element"
                });
                m_CurrentControls.Add(controlData);
            }
        }

        public override void OnDataChanged()
        {
            base.OnDataChanged();

            var node = GetPresenter<MaterialNodePresenter>();

            if (node == null)
            {
                m_ControlsContainer.Clear();
                m_CurrentControls.Clear();
                m_PreviewContainer.Clear();
                UpdatePreviewTexture(null);
                return;
            }

            UpdateControls(node);
            UpdatePreviewTexture(node.preview);

            if (node.expanded)
            {
                UpdatePreviewTexture(node.preview);
            }
            else
                m_PreviewContainer.Clear();
        }
    }
}
