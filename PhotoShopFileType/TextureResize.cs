// Single threaded version of http://wiki.unity3d.com/index.php/TextureScale#TextureScale.cs
// Unity3D wiki scripts are licensed under CC0, original author is Eric Haines (Eric5h5)

using UnityEngine;

public class TextureResize
{
	private readonly int texwidth, texheight;
	private readonly Color[] texColors;
	private readonly Texture2D inputTexture;

	public TextureResize(Texture2D input)
	{
		texColors = input.GetPixels();
		texwidth = input.width;
		texheight = input.height;
		inputTexture = input;
	}

	public Texture2D Resize(int newWidth, int newHeight)
	{
		Color[] newColors = new Color[newWidth * newHeight];
		float ratioX = 1.0f / ((float)newWidth / (texwidth - 1));
		float ratioY = 1.0f / ((float)newHeight / (texheight - 1));

		for (var y = 0; y < newHeight; y++)
		{
			int yFloor = (int)Mathf.Floor(y * ratioY);
			var y1 = yFloor * texwidth;
			var y2 = (yFloor + 1) * newWidth;
			var yw = y * newWidth;

			for (var x = 0; x < newWidth; x++)
			{
				int xFloor = (int)Mathf.Floor(x * ratioX);
				var xLerp = x * ratioX - xFloor;
				newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp),
													   ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp),
													   y * ratioY - yFloor);
			}
		}

		inputTexture.Resize(newWidth, newHeight);
		inputTexture.SetPixels(newColors);
		inputTexture.Apply();
		return inputTexture;
	}

	private Color ColorLerpUnclamped(Color c1, Color c2, float value)
	{
		return new Color(c1.r + (c2.r - c1.r) * value,
						  c1.g + (c2.g - c1.g) * value,
						  c1.b + (c2.b - c1.b) * value,
						  c1.a + (c2.a - c1.a) * value);
	}
}
