/* Public, source-authored probe for our CodeQL graph queries. */
typedef void (*operation)(void);
struct operations { operation row; operation unrelated; };

static void first_row(void) {}
static void second_row(void) {}
static void unrelated_operation(void) {}
static const struct operations first_table = { first_row, unrelated_operation };
static const struct operations second_table = { second_row, unrelated_operation };

void through_field(const struct operations *table)
{
    table->row();
}

void through_local(int choose_first)
{
    operation selected = choose_first ? first_table.row : second_table.row;
    selected();
}

/* Two expanded calls share an invocation location but need separate identities. */
#define TWO_CALLS() (first_row(), second_row())
void through_macro(void)
{
    TWO_CALLS();
}
