using HSVPicker;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image)), ExecuteInEditMode()]
public class ColorSliderImage : MonoBehaviour
{
    public ColorPicker picker;
    public ColorValues type;
    public Slider.Direction direction;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        // We use Simple type because we will generate the texture at the correct "stretched" size
        image.type = Image.Type.Simple;

        if (Application.isPlaying)
            RegenerateTexture();
    }

    private void OnEnable()
    {
        if (picker != null && Application.isPlaying)
        {
            picker.onValueChanged.AddListener(ColorChanged);
            picker.onHSVChanged.AddListener(HSVChanged);
        }
    }

    private void OnDisable()
    {
        if (picker != null)
        {
            picker.onValueChanged.RemoveListener(ColorChanged);
            picker.onHSVChanged.RemoveListener(HSVChanged);
        }
    }

    private void OnDestroy()
    {
        if (image != null && image.sprite != null && image.sprite.texture != null)
            DestroyImmediate(image.sprite.texture);
    }

    private void ColorChanged(Color newColor) => RegenerateTexture();

    private void HSVChanged(float h, float s, float v) => RegenerateTexture();

    private void RegenerateTexture()
    {
        if (image == null)
            image = GetComponent<Image>();

        Color32 baseColor = picker != null ? picker.CurrentColor : Color.black;
        float h = picker != null ? picker.H : 0;
        float s = picker != null ? picker.S : 0;
        float v = picker != null ? picker.V : 0;

        bool vertical =
            direction == Slider.Direction.BottomToTop || direction == Slider.Direction.TopToBottom;
        bool inverted =
            direction == Slider.Direction.TopToBottom || direction == Slider.Direction.RightToLeft;

        int size = type switch
        {
            ColorValues.R or ColorValues.G or ColorValues.B or ColorValues.A => 256,
            ColorValues.Hue => 360,
            ColorValues.Saturation or ColorValues.Value => 100,
            _ => 100,
        };

        // Instead of 1px wide, we make it 2px wide to ensure the UI Image stretches it correctly
        // without "slicing" or sampling errors. This replaces the need for uvRect.
        Texture2D texture = vertical ? new Texture2D(2, size) : new Texture2D(size, 2);
        texture.hideFlags = HideFlags.DontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < size; i++)
        {
            int colorIdx = inverted ? size - 1 - i : i;
            float percent = (float)i / (size - 1);

            Color32 col = type switch
            {
                ColorValues.R => new Color32((byte)i, baseColor.g, baseColor.b, 255),
                ColorValues.G => new Color32(baseColor.r, (byte)i, baseColor.b, 255),
                ColorValues.B => new Color32(baseColor.r, baseColor.g, (byte)i, 255),
                ColorValues.A => new Color32((byte)i, (byte)i, (byte)i, 255),
                ColorValues.Hue => HSVUtil.ConvertHsvToRgb(i, 1, 1, 1),
                ColorValues.Saturation => HSVUtil.ConvertHsvToRgb(h * 360, percent, v, 1),
                ColorValues.Value => HSVUtil.ConvertHsvToRgb(h * 360, s, percent, 1),
                _ => Color.white,
            };

            // Fill both pixels in the 2px thickness to prevent the "slice" look
            if (vertical)
            {
                texture.SetPixel(0, colorIdx, col);
                texture.SetPixel(1, colorIdx, col);
            }
            else
            {
                texture.SetPixel(colorIdx, 0, col);
                texture.SetPixel(colorIdx, 1, col);
            }
        }

        texture.Apply();

        if (image.sprite != null && image.sprite.texture != null)
            DestroyImmediate(image.sprite.texture);

        // Using a fixed PPU of 100 removes the ArgumentException risk.
        image.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100,
            0,
            SpriteMeshType.FullRect
        );
    }
}
