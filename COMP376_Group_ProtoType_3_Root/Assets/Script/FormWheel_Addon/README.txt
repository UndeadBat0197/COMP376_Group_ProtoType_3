FORM WHEEL ADD-ON (for existing Mouse / Human FormSwitcher)

1. Copy FormWheelUI.cs to Assets/Script/ in Unity.
2. Open a scene that contains working Mouse and Human GameObjects and FormSwitcher.
3. Select the separate always-active GameObject that holds FormSwitcher.
4. Add Component > FormWheelUI. Drag the FormSwitcher component to its Form Switcher field, or leave blank if both components are on the same GameObject.
5. In FormSwitcher assign mouseForm / humanForm and startAsHuman to match your intended initial character.
6. Press Play. Hold T: wheel opens. Move mouse into Human or Mouse wedge; release T to transform. Top Cat wedge is a locked placeholder.
7. If camera still rotates while selecting, temporarily disable the relevant Cinemachine/free-look input component while the wheel is open; camera input differs between project setups.

The script disables FormSwitcher's old T-toggle automatically while active. Other FormSwitcher logic is kept unchanged.
Do not put FormWheelUI on Mouse/Human because those GameObjects become inactive.
This is a lightweight IMGUI prototype wheel; Anonyme can later replace its visuals with Canvas UI without changing FormSwitcher.

NOTE: This cannot be play-tested in Unity here. Back up or branch before adding to shared main.
