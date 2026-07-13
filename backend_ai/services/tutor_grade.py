from __future__ import annotations

from config import Settings
from models.requests import TutorGradeRequest
from models.responses import TutorGradeResponse
from services.answer_grader import grade_user_answer
from services.quiz_bank import QuizBank


def grade_tutor_answer(req: TutorGradeRequest, bank: QuizBank, settings: Settings) -> TutorGradeResponse:
    qid = req.question_id.strip()
    row = bank.get(qid)
    if row is None:
        return TutorGradeResponse(
            is_correct=False,
            question_id=qid,
            reference_snippet="",
            quiz_complete_after=False,
            unknown_question=True,
        )

    locale = req.locale
    ok = grade_user_answer(
        req.user_answer,
        row.acceptable_answers_for(locale),
        fuzzy_ratio=settings.tutor_grade_fuzzy_ratio,
        fuzzy_max_len=settings.tutor_grade_fuzzy_max_len,
    )
    target = req.quiz_target
    new_count = req.correct_count_before + (1 if ok else 0)
    complete_after = ok and new_count >= target

    return TutorGradeResponse(
        is_correct=ok,
        question_id=row.question_id,
        reference_snippet=row.reference_snippet_for(locale),
        quiz_complete_after=complete_after,
        unknown_question=False,
    )
