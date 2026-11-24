import sys
sys.path.insert(0, r"D:\web_kurs\docs")

from doc_helpers import create_base_document
from part_front_and_sec1_2 import build_front_matter, build_section_1, build_section_2
from part_sec3_4 import build_section_3, build_section_4
from part_sec5_and_apps import build_section_5, build_conclusions_and_refs, build_appendices

OUT_DOCX = r"D:\web_kurs\Курсова_робота_Слюсаренко_АІ245_Варіант7.docx"

doc = create_base_document()
build_front_matter(doc)
build_section_1(doc)
build_section_2(doc)
build_section_3(doc)
build_section_4(doc)
build_section_5(doc)
build_conclusions_and_refs(doc)
build_appendices(doc)

doc.save(OUT_DOCX)
print("Successfully generated:", OUT_DOCX)
