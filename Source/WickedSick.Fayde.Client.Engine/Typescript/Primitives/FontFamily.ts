/// <reference path="../Runtime/Nullstone.ts" />
/// CODE

class FontFamily implements ICloneable {
    constructor(public FamilyNames: string) { }
    toString(): string {
        return this.FamilyNames;
    }
    Clone(): FontFamily {
        return new FontFamily(this.FamilyNames);
    }
}
Nullstone.RegisterType(FontFamily, "FontFamily");