using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Game.Ed
{
	public static class LayerMaskDrawer
	{
		public static int LayerMaskField(string label, int layermask)
		{
			return FieldToLayerMask(EditorGUILayout.MaskField(label, LayerMaskToField(layermask), InternalEditorUtility.layers));
		}

		public static int LayerMaskField(Rect position, string label, int layermask)
		{
			return FieldToLayerMask(EditorGUI.MaskField(position, label, LayerMaskToField(layermask), InternalEditorUtility.layers));
		}

		private static int FieldToLayerMask(int field)
		{
			if (field == -1) return -1;
			int mask = 0;
			var layers = InternalEditorUtility.layers;
			for (int c = 0; c < layers.Length; c++)
			{
				if ((field & (1 << c)) != 0)
				{
					mask |= 1 << LayerMask.NameToLayer(layers[c]);
				}
				else
				{
					mask &= ~(1 << LayerMask.NameToLayer(layers[c]));
				}
			}

			return mask;
		}

		private static int LayerMaskToField(int mask)
		{
			if (mask == -1) return -1;
			int field = 0;
			var layers = InternalEditorUtility.layers;
			for (int c = 0; c < layers.Length; c++)
			{
				if ((mask & (1 << LayerMask.NameToLayer(layers[c]))) != 0)
				{
					field |= 1 << c;
				}
			}

			return field;
		}
	}
}