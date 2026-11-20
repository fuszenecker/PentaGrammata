-- Pandoc filter for the A4 PDF user guides.
--  * Inline code (file names, paths, class names) may break after / . _ - and between a lower-
--    and an uppercase letter, and is never hyphenated, so long names wrap inside table cells.
--  * Links to the Markdown files (the other language, docs/README.md) mean nothing in a PDF:
--    the link text stays, the link goes; the "other language" note at the top is dropped.
--  * Horizontal rules between chapters are dropped: every chapter starts on a new page.
--  * The guide's own list of chapters is dropped: the PDF has its real table of contents.
--  * A heading is kept with what follows it: before a heading followed by a picture the page
--    must have room for the picture, otherwise for a few lines.
local escapes = {
  ["\\"] = "\\textbackslash{}", ["{"] = "\\{", ["}"] = "\\}", ["$"] = "\\$", ["&"] = "\\&",
  ["#"] = "\\#", ["^"] = "\\^{}", ["_"] = "\\_", ["%"] = "\\%", ["~"] = "\\textasciitilde{}",
  ["<"] = "\\textless{}", [">"] = "\\textgreater{}",
}

function Code(el)
  if not FORMAT:match("latex") then return nil end
  local out, previous = {}, ""
  for _, c in utf8.codes(el.text) do
    local ch = utf8.char(c)
    if ch:match("%u") and previous:match("%l") then table.insert(out, "\\allowbreak{}") end
    table.insert(out, escapes[ch] or ch)
    if ch:match("[/%._%-\\]") then table.insert(out, "\\allowbreak{}") end
    previous = ch
  end
  -- \leavevmode: at the start of a table cell the colour would otherwise open an empty line.
  return pandoc.RawInline("latex",
    "\\leavevmode{\\ttfamily\\hyphenchar\\font=-1 \\color{codefg}" .. table.concat(out) .. "}")
end

function Link(el)
  if el.target:match("%.md$") or el.target:match("%.md#") then return el.content end
end

function BlockQuote(el)
  local text = pandoc.utils.stringify(el)
  if text:match("^Magyar változat") or text:match("^English version") then return {} end
end

function HorizontalRule() return {} end

local function has_image(block)
  if not block then return false end
  if block.t == "Figure" then return true end
  if block.t == "Para" or block.t == "Plain" then
    for _, inline in ipairs(block.content) do
      if inline.t == "Image" then return true end
    end
  end
  return false
end

function Pandoc(doc)
  local blocks, skip_list = {}, false
  for i, block in ipairs(doc.blocks) do
    if block.t == "Header" then
      local text = pandoc.utils.stringify(block)
      if text == "Contents" or text == "Tartalom" then
        skip_list = true
        goto continue
      end
      if block.level >= 2 then
        local nextblock = doc.blocks[i + 1]
        local room = has_image(nextblock) and "0.5\\textheight"
          or (nextblock and nextblock.t == "Table") and "0.3\\textheight"
          or "6\\baselineskip"
        -- \needspace and then an explicit \clearpage when it decided to break: the break must
        -- happen here, before a longtable takes over the output routine, or the table head is
        -- printed alone at the top of the new page.
        table.insert(blocks, pandoc.RawBlock("latex", "\\keepwithnext{" .. room .. "}"))
      end
    elseif skip_list and block.t == "OrderedList" then
      skip_list = false
      goto continue
    end
    table.insert(blocks, block)
    ::continue::
  end
  doc.blocks = blocks
  return doc
end
