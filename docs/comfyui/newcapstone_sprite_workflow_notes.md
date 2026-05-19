# NewCapstone ComfyUI Sprite Workflow Notes

## Installed custom nodes

- `ComfyUI-AI-Pixel-Art-Enhancer`
- `ComfyUI_IPAdapter_plus`
- `comfyui_controlnet_aux`

## Workflow files

- `newcapstone_sprite_postprocess_api.json`
  - Immediately usable.
  - Put an image named `sprite_source.png` in `C:\Users\user\Documents\ComfyUI\input`.
  - Run the workflow to produce enhanced pixel output, comparison, and palette images.

- `newcapstone_sprite_consistency_sd15_api.json`
  - Intended for consistent character sprite candidate generation.
  - Put a reference image named `sprite_reference.png` in `C:\Users\user\Documents\ComfyUI\input`.
  - Requires IPAdapter SD1.5 model files before it can run.

## Required model files for consistency workflow

Place these files exactly as named:

- `C:\Users\user\Documents\ComfyUI\models\clip_vision\CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`
- `C:\Users\user\Documents\ComfyUI\models\ipadapter\ip-adapter_sd15.safetensors`

Optional next step for pose-locked animation frames:

- Add SD1.5 OpenPose or LineArt ControlNet models into `C:\Users\user\Documents\ComfyUI\models\controlnet`.
- Then extend the workflow with `DWPreprocessor` or `LineArtPreprocessor` plus `ControlNetApplyAdvanced`.

## Recommended production rule

Use the same `sprite_reference.png`, prompt skeleton, seed family, pixel settings, and palette output per character. For Unity import, treat the enhanced image as a candidate sheet source, then clean transparency and slice frames in `disputatio/Assets`.
