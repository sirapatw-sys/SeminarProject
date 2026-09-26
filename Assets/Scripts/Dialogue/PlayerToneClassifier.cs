using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>How a typed message treats the NPC it is said to.</summary>
public enum PlayerTone
{
    Neutral,

    /// <summary>Insults, orders to shut up or leave, blame, sarcasm.</summary>
    Hostile,

    /// <summary>Pushing away: refusing closeness, "none of your business".</summary>
    Cold,

    /// <summary>Greetings, care questions, interest in the NPC.</summary>
    Friendly,

    /// <summary>Thanks, apology, comfort, compliments, standing up for the NPC.</summary>
    Kind,
}

/// <summary>What the rules made of one message, and which rule decided it.</summary>
public struct ToneReading
{
    public PlayerTone Tone;
    public string Reason;

    public bool IsNegative
    {
        get { return Tone == PlayerTone.Hostile || Tone == PlayerTone.Cold; }
    }

    public bool IsPositive
    {
        get { return Tone == PlayerTone.Friendly || Tone == PlayerTone.Kind; }
    }

    public override string ToString()
    {
        return Tone + (string.IsNullOrEmpty(Reason) ? string.Empty : " (" + Reason + ")");
    }
}

/// <summary>
/// Reads the tone of a Thai (or simple English) message to an NPC with
/// rules, no network. It backs up the AI's judgement (RelationshipTuning.Judge)
/// and is the whole judgement when the AI is unavailable.
///
/// How it reads a message:
///   * every positive and negative signal is found, with its position;
///   * sarcasm (praise + a complaint, "ขอบคุณมากนะที่ไม่ช่วยอะไรเลย") is hostile outright;
///   * a signal inside a longer signal of the other kind belongs to it
///     ("เป็นห่วง" in "ไม่ต้องมาทำเป็นห่วง");
///   * otherwise the LAST signal wins, which handles contrast and order:
///     "ขอโทษนะ แต่เธอน่ารำคาญ" is negative, "ผมพูดว่าเธอน่ารำคาญ ขอโทษนะ" is not;
///   * judging words (แย่, โง่, น่ารำคาญ...) only count when aimed at the NPC:
///     "you" appears and the phrase is not about the room or the speaker, or
///     the message names no "I" and no room object at all ("แย่มาก");
///   * a denied judgement ("ไม่ได้น่ารำคาญ", "ไม่มีใครว่าเธออ่อนแอ") defends
///     the NPC; a past one ("เคยคิดว่า...") is not said now.
///
/// Thai has no spaces between words, so patterns carry guards against
/// matching inside other words (บ้า in บ้าง/บ้าน, แก in แกง/แกล้ง, คุณ in ขอบคุณ).
/// The test sentences live in Assets/Tests/Editor/PlayerToneTests.cs.
/// </summary>
public static class PlayerToneClassifier
{
    // ------------------------------------------------------------ words

    private const string You =
        "(?:เธอ|นาย(?!ก)|(?<!บ)แก(?![้มงละ่ว])|(?<!ขอบ)คุณ(?!ภาพ|ค่า|สมบัติ)|มึง|(?<!ด้วย)ตัวเอง|ยัย|เจ้า(?!ของ|หน้าที่)|ท่าน|\\byou\\b)";

    private static readonly Regex YouWord = R(You);

    // "เธอว่า..." / "เธอคิดว่า..." asks the NPC's opinion; it is not the
    // subject of whatever judging word follows.
    private static readonly Regex YouAsking = R(You + "(?:ว่า|คิดว่า|เห็นว่า|รู้สึกว่า)");

    private static readonly Regex MeWord = R("(?:ฉัน|ผม(?!ม)|เรา|หนู|\\bi\\b|\\bme\\b|\\bmy\\b)");

    // Things in the rooms; a judging word next to one is about the room.
    private static readonly Regex Topic = R(
        "(?:ห้อง|สถานการณ์|ปริศนา(?!ของ" + You + ")|ที่นี่|โลก|เสียง|ความมืด|ประตู|กระจก|กล่อง|นาฬิกา|" +
        "ภาพ|เก้าอี้|เตาผิง|ลิ้นชัก|หีบ|จารึก|หนังสือ|เพลง|ผี|เทียน|รหัส|อากาศ|ฝน|กุญแจ|โน้ต|บันทึก|" +
        "ตึก|บ้าน(?!" + You + ")|งาน|เกม|ด่าน|ทาง|ปัญหานี้|ความหนาว|ความเงียบ|กลไก|เฟือง|แผง|พรม|โต๊ะ|ตู้|ชั้น|แท่น|" +
        "บันได|ผนัง|หน้าต่าง|สมุด|ลาน|เงา|ลม|room|puzzle|door|this place)");

    // "เกลียด" / "รำคาญ" / "เบื่อ" take an object: "เกลียดเลขคณิต" hates maths,
    // "เกลียดเธอ" hates the NPC, "รำคาญชะมัด" has none and means the listener.
    private static readonly Regex Transitive = R("^(?:เกลียด|รำคาญ|เบื่อ|hate)$");

    private static readonly Regex StartsWithYou = R("^\\s?(?:หน้า)?" + You);

    private static readonly Regex OnlyIntensifiers = R(
        "^(?:ชะมัด|จริง(?:ๆ)?|จัง|มาก|เลย|ที่สุด|จะตาย|เป็นบ้า|นะ|ว่ะ|วะ|โว้ย|เหลือเกิน|สุดๆ|อ่ะ|อะ|ค่ะ|ครับ|แล้ว)*$");

    // "กลไกนี่", "ปริศนานี้": a thing pointed at. "เธอนี่" is the NPC.
    private static readonly Regex ThisThing = R("[ก-ฮa-z][^ ]{1,}?(?:นี่|นี้|นั่น|นั้น)(?:มัน)?$");

    // Frustration at the situation, not at anyone: "บ้าจริง ประตูไม่เปิด".
    private static readonly Regex Exclamation = R(
        "^(?:บ้าจริง|บ้าเอ๊ย|บ้าที่สุด|แย่จริง|แย่แล้ว|ให้ตาย(?:สิ|เถอะ)?|ซวยแล้ว|ตายแล้ว|โธ่เอ๊ย|โธ่)");

    // "ไม่ได้น่ารำคาญ" / "ไม่ใช่คนขี้แย": the insult is being denied.
    private const string NotBefore = "(?<!(?:ไม่|ไม่ได้|ไม่ใช่|ไม่ได้เป็น|ไม่เห็น)\\s?(?:เป็น)?(?:คน)?(?:น่า|ขี้|ตัว)?)";

    // Before a judging word in the same phrase: nobody is saying it now.
    private static readonly Regex Denied = R(
        "(?:ไม่มีใคร|ใคร)(?:ว่า|คิดว่า|บอกว่า)|ไม่ได้(?:คิด|ว่า|หมายความ|มองว่า)|ไม่คิดว่า|ไม่เคย(?:คิด|ว่า)");

    private static readonly Regex Past = R("เคย(?:คิด)?ว่า|ตอนแรก(?:คิด)?(?:ว่า)?|แต่ก่อน|เมื่อก่อน");

    private static readonly Regex Retracted = R("ตอนนี้ไม่|ไม่แล้ว|เปลี่ยนใจ|ตอนนี้(?:เชื่อ|รู้|เข้าใจ)");

    // Words that judge. Only an insult when aimed at the NPC (see Aimed).
    private static readonly Regex[] JudgingWords = Rs(NotBefore,
        "แย่(?!แล้ว|ละ|จัง|ลง|ที่สุดคือ)", "ห่วย", "น่าเบื่อ", "ไร้ประโยชน์", "ไม่มีประโยชน์", "ไร้สาระ", "เกลียด",
        "น่ารังเกียจ", "ขี้แย", "ขี้ขลาด", "ขี้บ่น", "ขี้โม้", "ขี้เก๊ก", "ขี้โกหก", "อ่อนแอ", "ปัญญาอ่อน", "ขยะ",
        "น่ารำคาญ", "(?<!น่า)รำคาญ", "น่าหงุดหงิด", "กวนใจ", "เกะกะ", "โง่", "บ้า(?![นง])", "น่าอาย", "น่าหมั่นไส้",
        "หยิ่ง", "หัวสูง", "เย็นชา", "ใจร้าย", "เห็นแก่ตัว", "ตัวปัญหา", "ตัวถ่วง", "(?:ตัว)?ภาระ", "ตัวซวย",
        "สาระแน", "ปากดี", "ปากเก่ง", "ไม่ได้เรื่อง", "ไม่(?:เห็น)?เอาไหน", "ไม่ได้ความ", "คนโกหก",
        "น่าสมเพช", "ไร้ค่า", "ขี้กลัว", "จอมปลอม", "หน้าไหว้หลังหลอก", "ดีแต่พูด", "เรื่องมาก", "จุกจิก",
        "กวนประสาท", "ตัวอันตราย", "ต้นเหตุ", "น่าสงสาร(?!นะ)", "งี่เง่า", "ไร้สมอง", "ขี้แพ้", "ทรยศ", "ตัวตลก",
        "เหมือนหมา", "ไม่ได้เรื่องได้ราว", "ไร้ความสามารถ", "เจ้าเล่ห์", "ขี้อ้าง", "ข้ออ้างเยอะ",
        "useless", "boring", "annoying", "stupid", "pathetic", "idiot", "selfish", "weak", "worst", "fake", "liar");

    // Compliments. Like insults, only when aimed at the NPC: "ประตูสวยแปลกๆ"
    // is about the gate, "ปากเก่ง" and "อย่ามาทำเป็นเก่ง" are not praise.
    private static readonly Regex[] PraiseWords = Rs("(?<!ไม่\\s?|ทำเป็น|ปาก|ขี้)",
        "เก่ง(?!แต่|นัก)", "ใจดี", "กล้า(?!ๆ)", "น่ารัก", "ฉลาด", "สุดยอด", "เท่(?!า)", "สวย", "หล่อ", "อบอุ่น", "ใจกว้าง",
        "(?<!ตัว)ตลก(?:ดี)?", "ใจเย็น", "ช่างสังเกต", "เสียงเพราะ", "สง่า", "มีเหตุผล", "เข้มแข็ง", "น่าสนใจ", "ดีมาก");

    // Rude whoever it is about (most carry "you" or are orders).
    private static readonly Regex[] HostilePhrases = Rs("",
        // orders to go away / shut up
        "หุบปาก", "ไสหัว", "ไปให้พ้น", "ไปตาย", "เสือก", "อย่ามายุ่ง", "ไม่ต้องมายุ่ง", "เงียบปาก", "เงียบซะ",
        "เงียบไปเลย", "หนวกหู", "ปากเสีย", "^(?:[^ ]+ )?เงียบ(?:ๆ)?$", "ไปไกลๆ", "(?:ออก)?ไปห่างๆ", "ถอยไป", "ไม่ต้องตามมา", "อย่ามา(?:คอย)?(?:ตาม|เกาะ|วอแว|ทำให้)",
        "(?:หยุด|เลิก)(?:พูด|บ่น|ร้องไห้|ร้อง|กรี๊ด|โวยวาย|ถาม|ตาม|เกาะ|เรียก)[^ ]{0,10}(?:สักที|ได้แล้ว|ซะ|เถอะ)",
        "หยุดทำตัว", "เลิกทำตัว", "เลิกตาม", "พูดมาก", "ถาม(?:เยอะ|มาก)", "ถาม(?:อะไร)?โง่", "ต้องให้พูดกี่รอบ",
        // nobody cares / wants you
        "ไม่มีใครถาม", "ใครสน", "ไม่มีใคร(?:อยาก)?(?:ฟัง|สน|ขอความเห็น|อยากคบ)", "ไม่แคร์", "nobody asked",
        "ไม่(?:ได้)?ต้องการ" + You, "ไม่อยาก(?:เห็นหน้า|เจอ|อยู่กับ)", "เบื่อ(?:จะฟัง|หน้า)?" + You, "เบื่อหน้า",
        "เบื่อจะฟัง", "เสียเวลา(?:คุย)?(?:กับ|ฟัง)", "คนอย่าง" + You, "เสียดายที่", "พูดกับ" + You + "แล้ว(?:เหนื่อย|ปวดหัว)",
        "ปวดหัว(?:กับ)?" + You, "ฟังแล้วปวดหัว", "ความรู้สึก(?:ของ)?" + You + "ไม่สำคัญ", "ไม่สนว่า" + You,
        "พูดไปก็เท่านั้น", You + "ไม่เข้าใจหรอก", "อย่ามาทำให้",
        // distrust, lies, blame
        "ไม่ไว้ใจ(?:คนอย่าง)?" + You, You + "(?:มัน)?(?:โกหก|หลอก)", "หลอก(?:ฉัน|ผม|เรา)", "อย่าคิดว่า(?:ฉัน|ผม)จะเชื่อ[^ ]{0,10}",
        "(?<!ไม่ใช่\\s?)ความผิด(?:ของ)?\\s?" + You, "เพราะ" + You + "(?:แท้ๆ|นั่นแหละ|คนเดียว)?", You + "(?:เป็น)?สาเหตุ",
        You + "ทำให้[^?]{0,24}(?:ยุ่งยาก|แย่|พัง|วุ่นวาย|เสีย|ติด)", You + "ทำ[^ ]{0,14}(?:พัง|เสีย|หาย)",
        "ใครใช้ให้" + You, "พอใจหรือยัง", "ทั้งหมดเป็นความผิด", "ช่างหัว" + You, "น่าอาย[^?]{0,10}ที่(?:กลัว|ร้อง)",
        // you're useless / better without you
        You + "(?:นี่)?(?:มัน)?(?:ก็)?(?:ไม่ได้|ไม่เห็น|ไม่เคย|ไม่)ช่วยอะไร", "(?:ไม่เห็น|ไม่)จะช่วยอะไร(?:ได้)?", "ทำอะไรเป็นบ้าง",
        "ทำไม(?:โง่|ช้า|ไม่เข้าใจ)", "(?:ถ้าไม่มี|ไม่มี)" + You + "[^?]{0,30}?(?:ดีกว่า|ดีเสียกว่า|นานแล้ว|สบาย)",
        "ถ้ารู้ว่า[^?]{0,20}" + You, "ไม่(?:ช่วย|เชื่อ)" + You + "(?:อีก|แล้ว)", "จะไม่ช่วย" + You,
        // mocking
        "(?:โตแล้ว|โตป่านนี้|แค่นี้|อายุปูนนี้)(?:แล้ว)?ยัง(?:กลัว|ร้องไห้|ร้อง|งอแง)", "แค่[^ ]{1,14}ยัง(?:กลัว|ร้อง)", "เอาแต่(?:กลัว|ร้อง|บ่น)",
        "ร้องไห้อยู่นั่นแหละ", "จะร้อง(?:ไห้)?ทำไม", "ร้องไปก็ไม่", "ไม่น่ากลัวเท่า(?:หน้า)?" + You,
        You + "(?:คิดว่า" + You + ")?เป็นใคร(?:ถึง|มา)", "ไม่มีสิทธิ์", "จำใส่หัว", "ถ้า" + You + "เงียบ",
        // do not presume
        "(?<!ไม่\\s?|ไม่มีวัน\\s?)ทิ้ง" + You + "ไว้", "ไม่ได้ขอความเห็น", "ไม่ต้องพูดดีกว่า",
        "อย่ามา(?:สั่ง|สอน|ทำเป็น[^ ]{0,10}|เสแสร้ง|พูดดี|ร้องไห้|บ่น|โวยวาย)",
        "ไม่ต้องมา(?:สอน|สั่ง|ทำเป็น[^ ]{0,10}|พูดดี|เสแสร้ง|เกะกะ|ทำหน้า)", "เล่ามาทำไม", "อยู่(?:ที่นี่)?คนเดียว(?:ได้ก็อยู่)?ไป",
        // more ways to say it
        You + "มีประโยชน์อะไร", "เหนื่อยกับ[^?]{0,24}" + You, "ทำไมต้อง(?:เป็น)?(?:ฉัน|ผม)[^?]{0,24}" + You,
        "เอาแต่(?:หลบ|กลัว|ร้อง|บ่น)", "(?:ไม่มี)?อะไรดี(?:ๆ)?จะพูด", "เงียบ(?:ๆ)?\\s?ไป(?:เถอะ|ซะ)", "ทำไมไม่เงียบ",
        "ไม่เข้าหู", "(?:อย่า(?:มา)?|ไม่ต้อง(?:มา)?|หยุด|เลิก)ทำ(?:เป็น|ดี|ตัว)[^ ]{0,10}", "ทำเป็น[^ ]{0,8}อยู่ได้",
        "(?:" + You + ")?ไม่มีวันเข้าใจ", "ไม่ต้อง(?:มา)?(?:สงสาร|ตอแย|ยิ้ม)", "อย่ามา(?:แตะ|ยิ้ม|เกี่ยว|ใกล้)",
        "อย่าเข้าใกล้(?:ฉัน|ผม)", "ใครจะ(?:ไป)?อยาก(?:คุย|อยู่)กับ", "(?:ช่วย)?ไป(?:ให้)?ไกล(?:ๆ)?(?:จาก)?(?:ฉัน|ผม)",
        "ไปไหนก็ไป", "ปล่อย(?:ฉัน|ผม)(?:ไว้)?(?:อยู่)?คนเดียว", "ใครถาม" + You, "ตั้งใจกวน",
        "ชอบทำให้[^?]{0,12}(?:หงุดหงิด|รำคาญ|เดือดร้อน)", "ประสาทจะกิน", You + "(?:นั่นแหละ)?ที่(?:ปลุก|ทำ)",
        "(?:ยัง|แม้แต่|ขนาด)[^?]{0,26}กว่า" + You, "ถ้า" + You + "ไม่(?:พูด|อยู่|มา|ตาม)", "พูดอยู่ได้", "พอได้แล้ว",
        "ฟังไม่ไหว", "เสแสร้ง", "ไว้ใจไม่ได้", "(?:พูด|ปาก)เก่ง", "เก่งแต่(?:ปาก|บ่น|พูด)", "(?:เก่ง|ดี|แน่|เจ๋ง)นัก(?:นะ)?",
        "shut up", "go away", "get lost", "hate you", "leave me alone", "don't trust you", "don't care about you",
        "หักหลัง", "ขายหน้า", "ผิดหวังใน(?:ตัว)?" + You, You + "ทำให้(?:ฉัน|ผม)(?:อาย|ขายหน้า|ผิดหวัง|เสียใจ)",
        "สมองมีไว้", "คิดได้แค่นี้", "ฟังไม่รู้เรื่อง", "เหมือนพูดกับกำแพง", "ไม่มีใครชอบ", "ทำไม" + You + "เป็นคนแบบนี้",
        "เลิกยุ่ง", "อย่ามา(?:เกะกะ|ขวาง)", You + "ทำให้[^?]{0,12}โกรธ", "ถ้า" + You + "ไม่(?:ไป)?(?:แตะ|ยุ่ง|เปิด|ทำ)",
        "stop following", "get away from", "your fault", "you broke", "i don't like you", "stop talking",
        "nobody likes you", "shut it",
        // more round-4 families
        You + "(?:นี่)?(?:มัน)?ไม่เคยทำอะไร(?:ให้)?(?:ถูก|ดี|ได้)", "(?:กรี๊ด|ร้อง|บ่น|ถาม|พูด)[^ ]{0,8}นักหนา",
        "เลิกทำหน้า", "ไม่เห็นว่า" + You + "จะมีประโยชน์", "เหม็นขี้หน้า", "(?:แค่นี้|ง่ายๆ แค่นี้)[^?]{0,10}(?:ทำไม่ได้|ทำไม่เป็น)",
        "ทำไมทำไม่เป็น", "ผิดหวัง(?:กับ|ใน)(?:ตัว)?" + You, You + "ทำให้(?:ทุกคน|เรา)(?:ลำบาก|ช้า|เดือดร้อน)",
        You + "(?:นี่)?แหละที่ทำให้", "อย่ามา(?:อ้าง|โกหก|หลอก)", "(?:พูด)?โกหกอีกแล้ว", "ไม่เชื่อ(?:สิ่งที่|ที่)?" + You + "พูด",
        "ไม่มีใครเชื่อ" + You, "ไม่ใช่พี่เลี้ยง", "ไม่ต้อง(?:มา)?พึ่ง(?:ฉัน|ผม)", You + "พึ่งตัวเองไม่ได้",
        "ไม่มี" + You + "ก็ได้", "ไม่(?:ได้)?ต้องการ(?:ความช่วยเหลือ|ให้" + You + ")", "ไปถามคนอื่น",
        "talk too much", "don't need you", "cold and rude", "so rude");

    // Pushing the NPC away without abuse.
    private static readonly Regex[] ColdPhrases = Rs("",
        "อย่ามาพูดเหมือน", "อย่า(?:มา)?ทำ(?:เป็น)?สนิท", "อย่ามาตีสนิท", "ไม่ได้สนิท", "ไม่ใช่เพื่อน",
        "ไม่ได้เป็นเพื่อน", "ไม่(?:ได้)?อยากเป็นเพื่อน", "เลิกเรียก", "อย่า(?:มา)?เรียก", "ไม่ต้องมาเรียก", "ไม่อยากคุย",
        "ไม่สน(?:ใจ)?(?:เรื่อง(?:ของ)?)?" + You, "ช่าง" + You + "(?:เถอะ|สิ)?", "ไม่ใช่(?:เรื่อง|กงการ)(?:อะไร)?ของ" + You,
        "ไม่เกี่ยวกับ" + You, You + "ไม่ต้องรู้", "เรื่องของ" + You + "(?:สิ|เถอะ)", "ก็เรื่องของ" + You,
        "อยู่ห่างๆ\\s?(?:ฉัน|ผม|เรา)", "ไม่(?:ได้)?อยาก(?:ฟัง|รู้)เรื่อง", "แล้วไง", "ไม่ต้องมา(?:ทำ)?(?:เป็น)?ห่วง",
        "ไม่(?:ค่อย)?ชอบ(?:วิธี|ท่าที)?(?:ที่)?" + You, "ไม่ได้เป็นอะไรกัน", "แค่บังเอิญ", "อย่าคิดไปไกล", "ไม่(?:ได้)?อยากคุย", You + "คิดว่า(?:ฉัน|ผม)อยาก",
        "ไม่ต้องบอก(?:ฉัน|ผม)", "ไม่ใช่ธุระ", "เรื่องส่วนตัว[^?]{0,6}ไม่ต้อง", "ไม่อยากตอบ",
        "not your friend", "none of your business");

    // Praise wrapped around a complaint.
    private static readonly Regex Sarcasm = R(
        "(?:ขอบคุณ|ขอบใจ|เก่ง|เยี่ยม|ยอด|ดีจริง|ดีจัง|ดีมาก|ดีใจด้วย|สุดยอด|ฉลาด|ช่วยได้(?:เยอะ|มาก)|thanks|great)" +
        "[^?]{0,30}?(?:ที่(?:" + You + ")?ไม่(?:ได้|เคย)?(?:ช่วย|ทำอะไร|สนใจ|บอก|ฟัง)|" +
        "ทำให้[^?]{0,22}?(?:แย่|พัง|ติด|ยุ่ง|เสีย|หลงทาง|โกรธ|ท้อ|กลัวกว่าเดิม)|" +
        "ทำ[^ ]{0,14}(?:พัง|ดับ|หาย)|พังหมด|พังจนได้|ผิด(?:ตั้ง|อีก)|ให้ท้อ|หลงทาง|ติดอยู่[^?]{0,16}(?:ตลอด|ต่อ)|โกรธแล้ว|เสียเวลา|" +
        "คำแนะนำไร้สาระ|ยากขึ้น|ลำบาก|ปลุกผี|ดับหมด|" +
        "for nothing|you broke|broke it)");

    private static readonly Regex[] KindPhrases = Rs("",
        // thanks, apology
        "ขอโทษ(?:นะ)?ที่[^ ]{0,40}", "ขอบคุณ(?:นะ)?ที่[^ ]{0,40}",
        "ขอบคุณ", "ขอบใจ", "thank", "ขอโทษ", "ขออภัย", "sorry", "ไม่ได้ตั้งใจ", "ผิดเองแหละ", "ไม่น่าพูด", "ใจร้อนไป",
        // comfort, promises
        "ไม่ต้องกลัว", "ไม่เป็นไร(?:นะ|หรอก)", "ไม่ต้อง(?:กังวล|คิดมาก|รู้สึกผิด)", "อยู่ตรงนี้", "อยู่นี่แล้ว", "อยู่ข้างๆ",
        "(?:จะ)?ไม่(?:มีวัน)?ทิ้ง", "(?:ไป|กลับ|ผ่าน|ออก|รอด)[^ ]{0,14}ด้วยกัน", "ปกป้อง", "จับมือ", "ร้องไห้(?:ได้|ออกมา)",
        "หายใจ(?:ลึกๆ|ช้าๆ)", "ค่อยๆ หายใจ", "สู้ๆ", "เป็นห่วง", "ระวัง", "ปลอดภัย", "เข้าใจ(?:ความรู้สึก|นะว่า|เลยว่า)",
        "ใจเย็นๆ", "ทุกอย่าง(?:จะ|ก็จะ)?(?:โอเค|ดีขึ้น)", "ใครๆ ก็", "สัญญา", "จะพา", "พา" + You + "กลับบ้าน",
        "ไม่ต้องห่วง", "ไม่แปลก", "รอ(?:จน)?" + You, "จัดการเอง",
        // compliments, trust, friendship
        "ดีใจที่", "โล่งใจที่", "ภูมิใจ", "โชคดี(?:นะ)?ที่มี", "(?<!ไม่\\s?)(?:เชื่อใจ|ไว้ใจ)(?!ได้ยังไง)", "ตอนนี้(?:เชื่อ|ไว้ใจ|เข้าใจ)",
        "(?:เพื่อน|พี่สาว|คน)ที่ดี", "พึ่งพาได้", "ทำได้ดี", "ช่วย[^ ]{0,8}ได้(?:เยอะ|มาก)", "(?<!ไม่\\s?)เชื่อ(?:ใจ)?" + You,
        "(?<!ไม่\\s?)เชื่อว่า" + You, You + "พูดถูก", "ควรฟัง" + You, "น่าจะฟัง" + You, "ชอบ(?:นิสัย|" + You + "|ตอนที่" + You + ")",
        "ยิ้ม[^?]{0,16}ก็ดี", "(?<!ไม่\\s?|ไม่ได้\\s?)(?:อยาก)?เป็นเพื่อน(?:กัน)?", "เยี่ยม(?:มาก|ไปเลย)",
        // defending the NPC
        "ไม่ใช่ความผิด", "ไม่ได้ว่า" + You, "ไม่ได้โกรธ" + You,
        "(?:ไม่|ไม่ได้|ไม่ใช่)\\s?(?:เป็น)?(?:คน)?(?:น่ารำคาญ|อ่อนแอ|โง่|ขี้แย|ขี้ขลาด|ไร้ประโยชน์|ตัวปัญหา|ภาระ)",
        // "without you I'd be lost" is gratitude, not "you are bad"
        "(?:ถ้า)?ไม่มี" + You + "[^?]{0,20}?(?:คง|จะ)[^?]{0,20}?(?:แย่|ไม่(?:ได้|ไหว|เจอ|รอด)|กลัว|หา[^ ]{0,12}ไม่เจอ)",
        "จะ(?:เดิน|อยู่|ไป)[^ ]{0,6}ข้างๆ", "ดูแล(?:เอง|ให้)", "จัดการให้", "ไม่(?:ยอม)?ปล่อยให้[^ ]{0,10}(?:ทำร้าย|เป็นอะไร)",
        "พิงไหล่", "อย่ากลัว", "ต้องผ่าน(?:ไป)?ได้", "ทำได้แน่", "โชคดีที่(?:ได้)?(?:เจอ|มี)", "ยกโทษ", "ผิดไปแล้ว",
        "ยินดีช่วย", "มีอะไรให้(?:ผม|ฉัน)ช่วย", "บอก(?:ผม|ฉัน)ได้", "อยู่ฝั่ง" + You, "เป็นทีมที่ดี", "เจอกันอีก",
        "เลี้ยงข้าว", "สบายใจขึ้น", "ทำให้(?:ผม|ฉัน)(?:ยิ้ม|กล้า|สบายใจ|หายกลัว)", "มี" + You + "แล้ว[^?]{0,20}ไม่น่ากลัว",
        "นับถือ", "เห็นด้วย(?:กับ)?" + You, "ค่อยๆ (?:เดิน|ไป|ทำ)", "(?:ผม|ฉัน)รอ", You + "ไม่ได้ทำ(?:อะไร)?ผิด",
        "ไม่มีใครรังเกียจ", "(?:ดู)?แล[^ ]{0,8}ดี(?:มาก)?", "ไม่ต้องรู้สึก[^ ]{0,6}", "เชื่อใน(?:ตัว)?" + You,
        You + "ไม่ได้ผิด", "ไม่มีใครโทษ", "กลัวแย่ถ้าไม่มี" + You, "ถ้าไม่(?:ได้)?" + You + "[^?]{0,20}?(?:คง|จะ)[^?]{0,20}?(?:ติด|แย่|ไม่รอด)",
        "\\bty\\b", "\\bcool\\b", "the best", "don't cry", "did great", "it's okay", "here for you",
        "(?:ผม|ฉัน)(?:ชอบ)(?:ที่|เวลา)" + You, "เคารพ", "(?:เดิน)?นำเอง", "(?:ผ่าน|ออก)ไป(?:ได้แน่|พร้อมกัน)",
        "ไม่ปล่อย(?:มือ)?" + You, "เรื่องเล็กน้อย", "เชื่อ(?:ผม|ฉัน)", "พักผ่อน", "kind", "glad you're", "you can do it",
        "don't be scared",
        "good job", "well done", "brave", "doing great", "don't worry", "i'm here", "you're great", "amazing",
        "proud of you", "believe in you", "take your time", "together", "nice to meet");

    private static readonly Regex[] FriendlyPhrases = Rs("",
        "สวัสดี", "หวัดดี", "ยินดีที่ได้(?:รู้จัก|เจอ)", "(?:โอเค|ไหว|เหนื่อย|หิว|หนาว|เจ็บ|เป็นอะไร)(?:อยู่)?(?:ไหม|มั้ย|หรือเปล่า|รึเปล่า|ป่ะ)",
        "เจ็บตรงไหน", "พัก(?:ก่อน|ตรงนี้)", "เล่า[^?]{0,30}ให้[^ ]{0,4}ฟัง", "ชอบทำอะไร", You + "ชอบ[^?]{0,16}(?:แบบไหน|อะไร)",
        "ชอบคุยกับ" + You, "อยากรู้จัก", "อยากรู้จัง", "ก่อนมาที่นี่" + You,
        "ไม่สบายใจ(?:ไหม|หรือเปล่า)", "มีอะไรไม่สบายใจ", "อยากคุยอะไร", "เห็นเงียบไป", "ดูเศร้า",
        You + "มี(?:พี่น้อง|ครอบครัว|แฟน)", You + "คิดถึงใคร", "บ้าน" + You + "เป็น(?:ยังไง|แบบไหน)",
        "ตอนเด็กๆ[^?]{0,6}" + You, You + "ตอนเด็กๆ", "ชอบกินอะไร", "หาย(?:เหนื่อย|กลัว)(?:หรือยัง)?",
        "ยัง(?:กลัว|หนาว|เจ็บ)อยู่(?:ไหม|หรือเปล่า)", "สบายดี(?:ไหม|หรือเปล่า)", "อยากฟังเรื่อง", "(?:ชอบ)?อ่านหนังสือแบบไหน",
        "hello", "\\bhi\\b", "how are you", "are you ok", "good morning", "good night");

    private static readonly Regex Chunks = R("[^\\s,.!?…~:;()\"']+");

    // "รำคาญที่เธอเดินตาม", "เบื่อกับเธอ": the NPC is what it is about.
    private static readonly Regex AboutYouNext = R("^[^ ]{0,4}(?:ที่|กับ|เพราะ|ใน)" + You);

    private static readonly Regex Repeats = new Regex("(.)\\1{2,}");

    // ------------------------------------------------------------ reading

    private struct Signal
    {
        public int Position;
        public int End;
        public PlayerTone Tone;
        public string Reason;
    }

    public static ToneReading Read(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ToneReading { Tone = PlayerTone.Neutral };
        }

        // "มากกกก" / "ขอบคุณณณ" read as the word itself.
        string text = Repeats.Replace(message.ToLowerInvariant().Replace("​", string.Empty), "$1");

        Match sarcasm = Sarcasm.Match(text);
        if (sarcasm.Success)
        {
            return new ToneReading { Tone = PlayerTone.Hostile, Reason = "sarcasm: " + sarcasm.Value };
        }

        List<Signal> signals = new List<Signal>();
        Collect(signals, text, HostilePhrases, PlayerTone.Hostile, "hostile");
        Collect(signals, text, ColdPhrases, PlayerTone.Cold, "cold");
        Collect(signals, text, KindPhrases, PlayerTone.Kind, "kind");
        Collect(signals, text, FriendlyPhrases, PlayerTone.Friendly, "friendly");
        FlipNegatedLiking(signals, text);
        CollectAimed(signals, text, JudgingWords, PlayerTone.Hostile, true);
        CollectAimed(signals, text, PraiseWords, PlayerTone.Kind, false);

        // "เป็นห่วง" inside "ไม่ต้องมาทำเป็นห่วง" is part of the longer phrase.
        signals.RemoveAll(inner => signals.Exists(outer =>
            IsPositive(outer.Tone) != IsPositive(inner.Tone) &&
            outer.Position <= inner.Position && inner.End <= outer.End &&
            outer.End - outer.Position > inner.End - inner.Position));

        if (signals.Count == 0)
        {
            return new ToneReading { Tone = PlayerTone.Neutral };
        }

        // The last thing said wins: "ขอบคุณนะ แต่ไม่ต้องมายุ่ง" ends pushing away.
        Signal last = signals[0];
        foreach (Signal signal in signals)
        {
            if (signal.Position > last.Position ||
                (signal.Position == last.Position && Weight(signal.Tone) > Weight(last.Tone)))
            {
                last = signal;
            }
        }

        return new ToneReading { Tone = last.Tone, Reason = last.Reason };
    }

    // "ไม่ชอบเธอ", "ไม่ได้อยากรู้จักเธอ", "ไม่ต้องเล่าให้ฟัง": liking or
    // interest said with "not" in front is a step away, not towards.
    // Thanks, apologies and comfort keep their "ไม่": "ไม่ต้องขอโทษ" is kind.
    private static readonly Regex Liking = R("ชอบ|รู้จัก|เล่า|ไว้ใจ|เชื่อ|เพื่อน|คุยกับ|สนใจ");

    private static readonly Regex NotRightBefore = R("(?:ไม่|ไม่ได้|ไม่ค่อย|ไม่เคย|ไม่ต้อง|ไม่น่า|ไม่มีใคร)(?:มา)?(?:อยาก)?\\s?$");

    private static readonly Regex NotRightAfter = R("^(?:" + You + ")?\\s?ไม่ได้");

    private static void FlipNegatedLiking(List<Signal> signals, string text)
    {
        for (int i = 0; i < signals.Count; i++)
        {
            Signal signal = signals[i];
            if (!IsPositive(signal.Tone))
            {
                continue;
            }

            string matched = text.Substring(signal.Position, signal.End - signal.Position);
            string before = text.Substring(Math.Max(0, signal.Position - 12), Math.Min(12, signal.Position));
            string after = text.Substring(signal.End, Math.Min(10, text.Length - signal.End));
            if (Liking.IsMatch(matched) && (NotRightBefore.IsMatch(before) || NotRightAfter.IsMatch(after)))
            {
                signal.Tone = PlayerTone.Cold;
                signal.Reason = "negated: " + matched;
                signals[i] = signal;
            }
        }
    }

    private static bool IsPositive(PlayerTone tone)
    {
        return tone == PlayerTone.Kind || tone == PlayerTone.Friendly;
    }

    /// <summary>At the same spot, a negative reading beats a positive one.</summary>
    private static int Weight(PlayerTone tone)
    {
        switch (tone)
        {
            case PlayerTone.Hostile: return 4;
            case PlayerTone.Cold: return 3;
            case PlayerTone.Kind: return 2;
            case PlayerTone.Friendly: return 1;
            default: return 0;
        }
    }

    private static void Collect(List<Signal> signals, string text, Regex[] patterns, PlayerTone tone, string label)
    {
        foreach (Regex pattern in patterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                signals.Add(new Signal
                {
                    Position = match.Index,
                    End = match.Index + match.Length,
                    Tone = tone,
                    Reason = label + ": " + match.Value,
                });
            }
        }
    }

    private static void CollectAimed(List<Signal> signals, string text, Regex[] words, PlayerTone tone, bool judging)
    {
        bool youInMessage = YouWord.IsMatch(YouAsking.Replace(text, string.Empty));
        bool meInMessage = MeWord.IsMatch(text);
        bool topicInMessage = Topic.IsMatch(text);

        foreach (Match chunk in Chunks.Matches(text))
        {
            string piece = chunk.Value;
            foreach (Regex pattern in words)
            {
                Match word = pattern.Match(piece);
                if (!word.Success)
                {
                    continue;
                }

                string before = piece.Substring(0, word.Index);
                string after = piece.Substring(word.Index + word.Length);
                int at = chunk.Index + word.Index;

                if (!judging)
                {
                    if (Aimed(piece, word.Index, after, youInMessage, meInMessage, topicInMessage))
                    {
                        signals.Add(new Signal { Position = at, End = at + word.Length, Tone = tone, Reason = "praise: " + word.Value + " in \"" + piece + "\"" });
                    }

                    continue;
                }

                // "ไม่มีใครว่าเธออ่อนแอ" / "ไม่ได้คิดว่าเธอน่ารำคาญ": defending her.
                if (Denied.IsMatch(before))
                {
                    signals.Add(new Signal { Position = at, End = at + word.Length, Tone = PlayerTone.Kind, Reason = "denied: " + piece });
                    continue;
                }

                // "เคยคิดว่าเธอน่ารำคาญ (แต่ตอนนี้ไม่แล้ว)": not said now.
                if (Past.IsMatch(before))
                {
                    Match retract = Retracted.Match(text, chunk.Index + chunk.Length);
                    if (retract.Success)
                    {
                        signals.Add(new Signal { Position = retract.Index, End = retract.Index + retract.Length, Tone = PlayerTone.Kind, Reason = "changed mind: " + retract.Value });
                    }

                    continue;
                }

                if (!Aimed(piece, word.Index, after, youInMessage, meInMessage, topicInMessage))
                {
                    continue;
                }

                signals.Add(new Signal
                {
                    Position = at,
                    End = at + word.Length,
                    Tone = PlayerTone.Hostile,
                    Reason = "insult: " + word.Value + " in \"" + piece + "\"",
                });
            }
        }
    }

    /// <summary>Is this judging word said about the NPC?</summary>
    private static bool Aimed(string piece, int wordAt, string after, bool youInMessage, bool meInMessage, bool topicInMessage)
    {
        string before = piece.Substring(0, wordAt);
        string asked = YouAsking.Replace(before, string.Empty);

        // "เธอนี่มันน่ารำคาญ..." — the NPC is the subject right there;
        // "รำคาญที่เธอ..." — the NPC is what it is about.
        if (YouWord.IsMatch(asked) || AboutYouNext.IsMatch(after))
        {
            return true;
        }

        if (Exclamation.IsMatch(piece))
        {
            return false;   // "บ้าจริง" / "ให้ตายสิ"
        }

        string word = piece.Substring(wordAt, piece.Length - wordAt - after.Length);
        if (Transitive.IsMatch(word) && !OnlyIntensifiers.IsMatch(after) && !StartsWithYou.IsMatch(after))
        {
            return false;   // its object is something other than the NPC
        }

        bool pieceAboutSomethingElse = Topic.IsMatch(piece) || MeWord.IsMatch(piece) ||
                                       (ThisThing.IsMatch(before) && !YouWord.IsMatch(before));

        // "รำคาญเธอจริงๆ", "...เธอพูดจาเหมือนหุ่นยนต์ น่าเบื่อจะตาย"
        if (youInMessage && !pieceAboutSomethingElse)
        {
            return true;
        }

        // "แย่มาก" said straight to the NPC: nothing else it could be about.
        return !meInMessage && !topicInMessage && !pieceAboutSomethingElse;
    }

    private static Regex R(string pattern)
    {
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static Regex[] Rs(string prefix, params string[] patterns)
    {
        Regex[] compiled = new Regex[patterns.Length];
        for (int i = 0; i < patterns.Length; i++)
        {
            compiled[i] = R(prefix + "(?:" + patterns[i] + ")");
        }

        return compiled;
    }
}
