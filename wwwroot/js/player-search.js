document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('playerSearch');
    const suggestionsContainer = document.getElementById('searchSuggestions');
    let debounceTimer;
    let currentSuggestions = [];

    if (!searchInput || !suggestionsContainer) {
        return;
    }


    searchInput.addEventListener('input', function() {
        const query = this.value.trim();
        
        
        clearTimeout(debounceTimer);
        
        if (query.length < 2) {
            hideSuggestions();
            return;
        }


        debounceTimer = setTimeout(() => {
            searchPlayers(query);
        }, 300);
    });


    searchInput.addEventListener('blur', function() {

        setTimeout(() => {
            hideSuggestions();
        }, 200);
    });


    suggestionsContainer.addEventListener('click', function(e) {
        const suggestionItem = e.target.closest('.suggestion-item');
        if (suggestionItem) {
            const playerId = suggestionItem.dataset.playerId;
            const playerName = suggestionItem.dataset.playerName;
            
            // manda a la pag de comp 
            window.location.href = `/Compare?player1=${encodeURIComponent(playerName)}`;
        }
    });

    // buscar
    async function searchPlayers(query) {
        try {
            const response = await fetch(`/api/player/search?query=${encodeURIComponent(query)}`);
            if (!response.ok) {
                throw new Error('Error en la búsqueda');
            }
            
            const suggestions = await response.json();
            currentSuggestions = suggestions;
            displaySuggestions(suggestions);
        } catch (error) {
            console.error('Error buscando jugadores:', error);
            hideSuggestions();
        }
    }

   // es para las sugerencias en la bsqueda
    function displaySuggestions(suggestions) {
        if (suggestions.length === 0) {
            hideSuggestions();
            return;
        }

        suggestionsContainer.innerHTML = '';
        
        suggestions.forEach(player => {
            const suggestionItem = document.createElement('div');
            suggestionItem.className = 'suggestion-item';
            suggestionItem.dataset.playerId = player.id;
            suggestionItem.dataset.playerName = player.fullName;
            
            suggestionItem.innerHTML = `
                <span class="player-name">${escapeHtml(player.fullName)}</span>
                <span class="player-team">${escapeHtml(player.team)} • ${escapeHtml(player.position)}</span>
            `;
            
            suggestionsContainer.appendChild(suggestionItem);
        });

        suggestionsContainer.style.display = 'block';
    }

    
    function hideSuggestions() {
        suggestionsContainer.style.display = 'none';
        suggestionsContainer.innerHTML = '';
    }


    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }


    searchInput.addEventListener('keydown', function(e) {
        const suggestions = suggestionsContainer.querySelectorAll('.suggestion-item');
        const activeSuggestion = suggestionsContainer.querySelector('.suggestion-item.active');
        
        if (suggestions.length === 0) {
            return;
        }

        switch(e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (activeSuggestion) {
                    activeSuggestion.classList.remove('active');
                    const next = activeSuggestion.nextElementSibling;
                    if (next) {
                        next.classList.add('active');
                    } else {
                        suggestions[0].classList.add('active');
                    }
                } else {
                    suggestions[0].classList.add('active');
                }
                break;
                
            case 'ArrowUp':
                e.preventDefault();
                if (activeSuggestion) {
                    activeSuggestion.classList.remove('active');
                    const prev = activeSuggestion.previousElementSibling;
                    if (prev) {
                        prev.classList.add('active');
                    } else {
                        suggestions[suggestions.length - 1].classList.add('active');
                    }
                } else {
                    suggestions[suggestions.length - 1].classList.add('active');
                }
                break;
                
            case 'Enter':
                e.preventDefault();
                if (activeSuggestion) {
                    const playerId = activeSuggestion.dataset.playerId;
                    const playerName = activeSuggestion.dataset.playerName;
                    window.location.href = `/Compare?player1=${encodeURIComponent(playerName)}`;
                }
                break;
                
            case 'Escape':
                hideSuggestions();
                searchInput.blur();
                break;
        }
    });
});
