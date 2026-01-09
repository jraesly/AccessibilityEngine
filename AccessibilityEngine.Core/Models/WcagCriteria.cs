namespace AccessibilityEngine.Core.Models;

/// <summary>
/// WCAG 2.2 Success Criteria enumeration for standards traceability.
/// Section 508 compliance requires Level A and AA criteria per E205.4 and E207.2.
/// </summary>
public enum WcagCriterion
{
    None = 0,

    // Principle 1: Perceivable
    // Guideline 1.1 - Text Alternatives
    /// <summary>1.1.1 Non-text Content (Level A)</summary>
    NonTextContent_1_1_1,

    // Guideline 1.2 - Time-based Media
    /// <summary>1.2.1 Audio-only and Video-only (Prerecorded) (Level A)</summary>
    AudioOnlyVideoOnly_1_2_1,
    /// <summary>1.2.2 Captions (Prerecorded) (Level A)</summary>
    CaptionsPrerecorded_1_2_2,
    /// <summary>1.2.3 Audio Description or Media Alternative (Prerecorded) (Level A)</summary>
    AudioDescriptionMediaAlternative_1_2_3,
    /// <summary>1.2.4 Captions (Live) (Level AA)</summary>
    CaptionsLive_1_2_4,
    /// <summary>1.2.5 Audio Description (Prerecorded) (Level AA)</summary>
    AudioDescriptionPrerecorded_1_2_5,

    // Guideline 1.3 - Adaptable
    /// <summary>1.3.1 Info and Relationships (Level A)</summary>
    InfoAndRelationships_1_3_1,
    /// <summary>1.3.2 Meaningful Sequence (Level A)</summary>
    MeaningfulSequence_1_3_2,
    /// <summary>1.3.3 Sensory Characteristics (Level A)</summary>
    SensoryCharacteristics_1_3_3,
    /// <summary>1.3.4 Orientation (Level AA)</summary>
    Orientation_1_3_4,
    /// <summary>1.3.5 Identify Input Purpose (Level AA)</summary>
    IdentifyInputPurpose_1_3_5,

    // Guideline 1.4 - Distinguishable
    /// <summary>1.4.1 Use of Color (Level A)</summary>
    UseOfColor_1_4_1,
    /// <summary>1.4.2 Audio Control (Level A)</summary>
    AudioControl_1_4_2,
    /// <summary>1.4.3 Contrast (Minimum) (Level AA)</summary>
    ContrastMinimum_1_4_3,
    /// <summary>1.4.4 Resize Text (Level AA)</summary>
    ResizeText_1_4_4,
    /// <summary>1.4.5 Images of Text (Level AA)</summary>
    ImagesOfText_1_4_5,
    /// <summary>1.4.10 Reflow (Level AA)</summary>
    Reflow_1_4_10,
    /// <summary>1.4.11 Non-text Contrast (Level AA)</summary>
    NonTextContrast_1_4_11,
    /// <summary>1.4.12 Text Spacing (Level AA)</summary>
    TextSpacing_1_4_12,
    /// <summary>1.4.13 Content on Hover or Focus (Level AA)</summary>
    ContentOnHoverOrFocus_1_4_13,

    // Principle 2: Operable
    // Guideline 2.1 - Keyboard Accessible
    /// <summary>2.1.1 Keyboard (Level A)</summary>
    Keyboard_2_1_1,
    /// <summary>2.1.2 No Keyboard Trap (Level A)</summary>
    NoKeyboardTrap_2_1_2,
    /// <summary>2.1.4 Character Key Shortcuts (Level A)</summary>
    CharacterKeyShortcuts_2_1_4,

    // Guideline 2.2 - Enough Time
    /// <summary>2.2.1 Timing Adjustable (Level A)</summary>
    TimingAdjustable_2_2_1,
    /// <summary>2.2.2 Pause, Stop, Hide (Level A)</summary>
    PauseStopHide_2_2_2,

    // Guideline 2.3 - Seizures and Physical Reactions
    /// <summary>2.3.1 Three Flashes or Below Threshold (Level A)</summary>
    ThreeFlashesOrBelowThreshold_2_3_1,

    // Guideline 2.4 - Navigable
    /// <summary>2.4.1 Bypass Blocks (Level A)</summary>
    BypassBlocks_2_4_1,
    /// <summary>2.4.2 Page Titled (Level A)</summary>
    PageTitled_2_4_2,
    /// <summary>2.4.3 Focus Order (Level A)</summary>
    FocusOrder_2_4_3,
    /// <summary>2.4.4 Link Purpose (In Context) (Level A)</summary>
    LinkPurposeInContext_2_4_4,
    /// <summary>2.4.5 Multiple Ways (Level AA)</summary>
    MultipleWays_2_4_5,
    /// <summary>2.4.6 Headings and Labels (Level AA)</summary>
    HeadingsAndLabels_2_4_6,
    /// <summary>2.4.7 Focus Visible (Level AA)</summary>
    FocusVisible_2_4_7,
    /// <summary>2.4.11 Focus Not Obscured (Minimum) (Level AA) - New in WCAG 2.2</summary>
    FocusNotObscuredMinimum_2_4_11,

    // Guideline 2.5 - Input Modalities
    /// <summary>2.5.1 Pointer Gestures (Level A)</summary>
    PointerGestures_2_5_1,
    /// <summary>2.5.2 Pointer Cancellation (Level A)</summary>
    PointerCancellation_2_5_2,
    /// <summary>2.5.3 Label in Name (Level A)</summary>
    LabelInName_2_5_3,
    /// <summary>2.5.4 Motion Actuation (Level A)</summary>
    MotionActuation_2_5_4,
    /// <summary>2.5.7 Dragging Movements (Level AA) - New in WCAG 2.2</summary>
    DraggingMovements_2_5_7,
    /// <summary>2.5.8 Target Size (Minimum) (Level AA) - New in WCAG 2.2</summary>
    TargetSizeMinimum_2_5_8,

    // Principle 3: Understandable
    // Guideline 3.1 - Readable
    /// <summary>3.1.1 Language of Page (Level A)</summary>
    LanguageOfPage_3_1_1,
    /// <summary>3.1.2 Language of Parts (Level AA)</summary>
    LanguageOfParts_3_1_2,

    // Guideline 3.2 - Predictable
    /// <summary>3.2.1 On Focus (Level A)</summary>
    OnFocus_3_2_1,
    /// <summary>3.2.2 On Input (Level A)</summary>
    OnInput_3_2_2,
    /// <summary>3.2.3 Consistent Navigation (Level AA)</summary>
    ConsistentNavigation_3_2_3,
    /// <summary>3.2.4 Consistent Identification (Level AA)</summary>
    ConsistentIdentification_3_2_4,
    /// <summary>3.2.6 Consistent Help (Level A) - New in WCAG 2.2</summary>
    ConsistentHelp_3_2_6,

    // Guideline 3.3 - Input Assistance
    /// <summary>3.3.1 Error Identification (Level A)</summary>
    ErrorIdentification_3_3_1,
    /// <summary>3.3.2 Labels or Instructions (Level A)</summary>
    LabelsOrInstructions_3_3_2,
    /// <summary>3.3.3 Error Suggestion (Level AA)</summary>
    ErrorSuggestion_3_3_3,
    /// <summary>3.3.4 Error Prevention (Legal, Financial, Data) (Level AA)</summary>
    ErrorPreventionLegalFinancialData_3_3_4,
    /// <summary>3.3.7 Redundant Entry (Level A) - New in WCAG 2.2</summary>
    RedundantEntry_3_3_7,
    /// <summary>3.3.8 Accessible Authentication (Minimum) (Level AA) - New in WCAG 2.2</summary>
    AccessibleAuthenticationMinimum_3_3_8,

    // Principle 4: Robust
    // Guideline 4.1 - Compatible
    /// <summary>4.1.2 Name, Role, Value (Level A)</summary>
    NameRoleValue_4_1_2,
    /// <summary>4.1.3 Status Messages (Level AA)</summary>
    StatusMessages_4_1_3
}

/// <summary>
/// WCAG conformance levels.
/// </summary>
public enum WcagLevel
{
    /// <summary>Level A - Minimum accessibility</summary>
    A,
    /// <summary>Level AA - Standard accessibility (required for Section 508)</summary>
    AA,
    /// <summary>Level AAA - Enhanced accessibility</summary>
    AAA
}

/// <summary>
/// Extension methods for WCAG criteria.
/// </summary>
public static class WcagCriterionExtensions
{
    /// <summary>
    /// Gets the conformance level for a WCAG criterion.
    /// </summary>
    public static WcagLevel GetLevel(this WcagCriterion criterion)
    {
        return criterion switch
        {
            // Level AA criteria
            WcagCriterion.CaptionsLive_1_2_4 => WcagLevel.AA,
            WcagCriterion.AudioDescriptionPrerecorded_1_2_5 => WcagLevel.AA,
            WcagCriterion.Orientation_1_3_4 => WcagLevel.AA,
            WcagCriterion.IdentifyInputPurpose_1_3_5 => WcagLevel.AA,
            WcagCriterion.ContrastMinimum_1_4_3 => WcagLevel.AA,
            WcagCriterion.ResizeText_1_4_4 => WcagLevel.AA,
            WcagCriterion.ImagesOfText_1_4_5 => WcagLevel.AA,
            WcagCriterion.Reflow_1_4_10 => WcagLevel.AA,
            WcagCriterion.NonTextContrast_1_4_11 => WcagLevel.AA,
            WcagCriterion.TextSpacing_1_4_12 => WcagLevel.AA,
            WcagCriterion.ContentOnHoverOrFocus_1_4_13 => WcagLevel.AA,
            WcagCriterion.MultipleWays_2_4_5 => WcagLevel.AA,
            WcagCriterion.HeadingsAndLabels_2_4_6 => WcagLevel.AA,
            WcagCriterion.FocusVisible_2_4_7 => WcagLevel.AA,
            WcagCriterion.FocusNotObscuredMinimum_2_4_11 => WcagLevel.AA,
            WcagCriterion.DraggingMovements_2_5_7 => WcagLevel.AA,
            WcagCriterion.TargetSizeMinimum_2_5_8 => WcagLevel.AA,
            WcagCriterion.LanguageOfParts_3_1_2 => WcagLevel.AA,
            WcagCriterion.ConsistentNavigation_3_2_3 => WcagLevel.AA,
            WcagCriterion.ConsistentIdentification_3_2_4 => WcagLevel.AA,
            WcagCriterion.ErrorSuggestion_3_3_3 => WcagLevel.AA,
            WcagCriterion.ErrorPreventionLegalFinancialData_3_3_4 => WcagLevel.AA,
            WcagCriterion.AccessibleAuthenticationMinimum_3_3_8 => WcagLevel.AA,
            WcagCriterion.StatusMessages_4_1_3 => WcagLevel.AA,
            
            // Everything else is Level A
            _ => WcagLevel.A
        };
    }

    /// <summary>
    /// Gets a human-readable reference string for the criterion.
    /// </summary>
    public static string ToReferenceString(this WcagCriterion criterion)
    {
        var level = criterion.GetLevel();
        return criterion switch
        {
            WcagCriterion.NonTextContent_1_1_1 => $"WCAG 2.2 § 1.1.1 Non-text Content (Level {level})",
            WcagCriterion.InfoAndRelationships_1_3_1 => $"WCAG 2.2 § 1.3.1 Info and Relationships (Level {level})",
            WcagCriterion.MeaningfulSequence_1_3_2 => $"WCAG 2.2 § 1.3.2 Meaningful Sequence (Level {level})",
            WcagCriterion.UseOfColor_1_4_1 => $"WCAG 2.2 § 1.4.1 Use of Color (Level {level})",
            WcagCriterion.ContrastMinimum_1_4_3 => $"WCAG 2.2 § 1.4.3 Contrast (Minimum) (Level {level})",
            WcagCriterion.NonTextContrast_1_4_11 => $"WCAG 2.2 § 1.4.11 Non-text Contrast (Level {level})",
            WcagCriterion.Keyboard_2_1_1 => $"WCAG 2.2 § 2.1.1 Keyboard (Level {level})",
            WcagCriterion.NoKeyboardTrap_2_1_2 => $"WCAG 2.2 § 2.1.2 No Keyboard Trap (Level {level})",
            WcagCriterion.TimingAdjustable_2_2_1 => $"WCAG 2.2 § 2.2.1 Timing Adjustable (Level {level})",
            WcagCriterion.FocusOrder_2_4_3 => $"WCAG 2.2 § 2.4.3 Focus Order (Level {level})",
            WcagCriterion.HeadingsAndLabels_2_4_6 => $"WCAG 2.2 § 2.4.6 Headings and Labels (Level {level})",
            WcagCriterion.FocusVisible_2_4_7 => $"WCAG 2.2 § 2.4.7 Focus Visible (Level {level})",
            WcagCriterion.TargetSizeMinimum_2_5_8 => $"WCAG 2.2 § 2.5.8 Target Size (Minimum) (Level {level})",
            WcagCriterion.LanguageOfPage_3_1_1 => $"WCAG 2.2 § 3.1.1 Language of Page (Level {level})",
            WcagCriterion.ErrorIdentification_3_3_1 => $"WCAG 2.2 § 3.3.1 Error Identification (Level {level})",
            WcagCriterion.LabelsOrInstructions_3_3_2 => $"WCAG 2.2 § 3.3.2 Labels or Instructions (Level {level})",
            WcagCriterion.NameRoleValue_4_1_2 => $"WCAG 2.2 § 4.1.2 Name, Role, Value (Level {level})",
            WcagCriterion.StatusMessages_4_1_3 => $"WCAG 2.2 § 4.1.3 Status Messages (Level {level})",
            _ => $"WCAG 2.2 (Level {level})"
        };
    }
}
